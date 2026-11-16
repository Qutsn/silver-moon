# Middleton 蓝牙通道与 Windows 实现

本文范围为项目已研究的 Middleton / LENNOX，主要基于官方 17.0.37。UUID、协议格式和实验结论不应直接推广到其他 Marshall 型号。

## 音频与 BLE 管理不是同一条通道

同一台音箱可以在 Windows 中出现音频设备及带 LE 后缀的设备。经典蓝牙音频负责播放；BLE 的 GATT 服务提供设备信息、声音控制及升级通信。两个条目不代表两台音箱，连接管理通道也不等于已经连接音频播放通道。

用户可能改名，BLE 地址也可能变化。软件扫描可连接的 LE 设备并让用户选择，再读取型号、制造商、能力等判断支持情况；不依赖显示名称包含 Middleton。型号字符串、序列号和 UUID 是兼容性与连接连续性依据，不是厂商身份的密码学认证。

## GATT 服务

| 通道 | 特征 / 服务 | 用途 |
|---|---|---|
| 设备信息 | `2a24` / `2a25` / `2a26` / `2a27` | 型号、序列号、固件、硬件 |
| 电量 | `2a19` | Battery Level |
| 声音设置 | `aa00` 服务下 `aa08`、`aa16` | 音量、两段 EQ，支持读取、通知及写入 |
| GAIA 升级 | `00001100-d102-11e1-9b23-00025b00a5a5` | 升级服务 |
| GAIA 写入 | 同一长 UUID 的 `1101` | 命令写入 |
| GAIA 通知 | 同一长 UUID 的 `1102`、`1103` | 响应与数据通知 |

前面短 UUID 补全为 `0000xxxx-0000-1000-8000-00805f9b34fb`。声音接口与 GAIA 升级接口应分别处理，不能把升级 START 返回值当作已证实的电量百分比。

GAIA 帧包含大端 vendor、command 和 payload。升级控制使用 `0x0642`；响应命令为原命令 OR `0x8000`，状态 0 表示成功。异步升级通知另外分发。客户端采用最多 45 字节固件内容块并逐帧等 ACK；历史实验中增大块长曾停滞，不能仅按 ATT MTU 推算可靠吞吐。

## Windows 端如何连接

软件使用 Windows BLE/GATT API，维护 GattSession、服务及特征生命周期。发现设备、请求访问、身份核对、保护级别协商、受保护读取与通知订阅是不同步骤；“已配对”不代表每个服务当前可达。

[PairingNegotiation](../glass-playground/Application/PairingNegotiation.cs) 只在 ProtectionLevelCouldNotBeMet 时尝试较低保护要求；AccessDenied、取消或超时不会被解释成需要无限降级。读取和写入还要经过 [ProtectedGatt](../glass-playground/Application/ProtectedGatt.cs) 的认证门控。

项目曾遇到“同一程序换目录后首次配对成功”。后来发现失败副本继承 Low 完整性标签，运行进程为 Low；成功副本为 Medium。仅检查普通 ACL 或是否管理员不足以区分。发布脚本通过新建副本和完整性标签检查避免带入此问题，不要求用户提权或修改系统蓝牙配置。这个证据不意味着所有 AccessDenied 都由此引起。

音箱升级重启后，旧会话曾反复缺失特征，新进程却能重新发现。因此桌面升级使用 [UpgradeWorker](../glass-playground/Infrastructure/UpgradeWorker.cs) 管理独立子进程，每次重连释放旧资源并建立新会话。这是生命周期处理，不是权限隔离，也不是所有 Windows 蓝牙问题的通用修复。

## 声音控制与读回

音量为 **0–32 档**，向 `aa08` 写单字节；不是 0–255、百分比或 dB。两段 EQ 协议为 0–10，向 `aa16` 写：

```text
[Bass, FF, FF, FF, Treble, 00, 00, 00]
```

本项目 UI 的 EQ 调节范围仍为 1–10，但允许显示设备读回的 0；音量允许调到 0。修改单个 EQ 频段时保留另一频段，避免连续操作相互覆盖。实体按键的变化通过通知进入界面；写入返回、目标值及实际设备报告是不同状态。

音量读回存在以下行为：刚连接且尚未播放时，设备音量可能上报 0，即使听感对应的原音量并非 0；开始播放或操作音箱实体音量键后才同步。不能把这个 0 一律归为 UI 解析错误，也不应擅自编造一个“真实值”。EQ 能单独读取，不能证明音量接口始终处于相同状态。

## 代码入口

[DeviceConnection](../glass-playground/Application/DeviceConnection.cs) · [WindowsBleTransport](../glass-playground/Infrastructure/WindowsBleTransport.cs) · [DeviceProfiles](../glass-playground/Domain/DeviceProfiles.cs) · [SoundControl](../glass-playground/Application/SoundControl.cs) · [SoundWriteQueue](../glass-playground/Application/SoundWriteQueue.cs)
