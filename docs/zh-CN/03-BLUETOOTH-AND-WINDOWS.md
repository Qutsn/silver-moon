# Middleton 蓝牙通道与 Windows 实现

[English](../en/03-BLUETOOTH-AND-WINDOWS.md) · [文档目录](README.md)

本文范围为项目已研究的 Middleton / LENNOX，主要基于官方 17.0.37。UUID、协议格式和实验结论不应直接推广到其他 Marshall 型号。

## 经典蓝牙音频与 BLE 管理通道

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

GAIA 帧包含大端编码的 vendor、command 字段和消息相关的 payload；payload 中的多字节字段按对应消息定义解析。升级控制使用 `0x0642`；响应命令为原命令 OR `0x8000`，状态 0 表示成功。异步升级通知另外分发。客户端采用最多 45 字节固件内容块并逐帧等 ACK；历史实验中增大块长曾停滞，不能仅按 ATT MTU 推算可靠吞吐。

## Windows 连接与认证流程

软件使用 Windows BLE/GATT API，维护 GattSession、服务及特征生命周期。发现设备、请求访问、身份核对、保护级别协商、受保护读取与通知订阅是不同步骤；“已配对”不代表每个服务当前可达。

[PairingNegotiation](../../src/SilverMoon/Application/PairingNegotiation.cs) 只在 ProtectionLevelCouldNotBeMet 时尝试较低保护要求；AccessDenied、取消或超时不会被解释成需要无限降级。读取和写入还要经过 [ProtectedGatt](../../src/SilverMoon/Application/ProtectedGatt.cs) 的认证门控。

项目曾遇到“同一程序换目录后首次配对成功”。后来发现失败副本继承 Low 完整性标签，运行进程为 Low；成功副本为 Medium。仅检查普通 ACL 或是否管理员不足以区分。发布脚本通过新建副本和完整性标签检查避免带入此问题，不要求用户提权或修改系统蓝牙配置。这个证据不意味着所有 AccessDenied 都由此引起。

音箱升级重启后，旧会话曾反复缺失特征，新进程却能重新发现。因此桌面升级使用 [UpgradeWorker](../../src/SilverMoon/Infrastructure/UpgradeWorker.cs) 管理独立子进程，每次重连释放旧资源并建立新会话。这是生命周期处理，不是权限隔离，也不是所有 Windows 蓝牙问题的通用修复。

## 声音控制与读回

音量为 **0–32 档**，向 `aa08` 写单字节；不是 0–255、百分比或 dB。两段 EQ 协议为 0–10，向 `aa16` 写：

```text
[Bass, FF, FF, FF, Treble, 00, 00, 00]
```

本项目 UI 的 EQ 调节范围仍为 1–10，但允许显示设备读回的 0；音量允许调到 0。修改单个 EQ 频段时保留另一频段，避免连续操作相互覆盖。实体按键的变化通过通知进入界面；写入返回、目标值及实际设备报告是不同状态。

音量读回存在以下行为：刚连接且尚未播放时，设备音量可能上报 0，即使听感对应的原音量并非 0；开始播放或操作音箱实体音量键后可能恢复同步。不能把这个 0 一律归为 UI 解析错误，也不应擅自编造一个“真实值”。EQ 能单独读取，不能证明音量接口始终处于相同状态。

## 代码入口

[DeviceConnection](../../src/SilverMoon/Application/DeviceConnection.cs) · [WindowsBleTransport](../../src/SilverMoon/Infrastructure/WindowsBleTransport.cs) · [DeviceProfiles](../../src/SilverMoon/Domain/DeviceProfiles.cs) · [SoundControl](../../src/SilverMoon/Application/SoundControl.cs) · [SoundWriteQueue](../../src/SilverMoon/Application/SoundWriteQueue.cs)

## BLE GAIA 帧与消息关联

[UpgradeWire](../../src/SilverMoon/Domain/UpgradeWire.cs) 实现以下表示，多字节整数为大端：

| 帧 | 编码 |
|---|---|
| 命令 | vendor 0x000A（2 字节）、command（2 字节）、payload |
| 升级控制 | command=0x0642；payload 为 1 字节消息号、2 字节 body 长度、body |
| 命令 ACK | 原 command OR 0x8000；payload 首字节为状态（0 成功），后续为可选数据 |
| 升级通知 | command=0x4003；event=0x12，随后消息号、2 字节 body 长度及 body |
| 数据请求 | 消息 3，body 为 4 字节 count 和 4 字节 skip |
| 验证等待 | 消息 23，body 为 2 字节毫秒数 |

解析器校验 vendor、最小帧长和通知 body 长度。UpgradeMailbox 只允许一个待完成命令，要求 ACK 命令号匹配；异步升级消息另存于容量 256 的队列。意外 ACK 或队列溢出会将该会话标记为不可继续使用，不会丢掉事件后继续安装。每个 mailbox 仅归属一个连接；断线或超时导致关联不明时不能复用。

当前支持判断的具体条件是型号 MIDDLETON 加必需的 GAIA 写入/响应特征。制造商字符串只用于区分其他未支持的 Marshall/Zound 设备。配对保护协商按 EncryptionAndAuthentication、Encryption、Default 顺序尝试，只有前一次为 ProtectionLevelCouldNotBeMet 才继续。

## 传输耗时与吞吐量

已有 Windows 测试记录中，全量固件传输约需 **45–50 分钟**。建议为传输及安装至少预留 **一小时**；中断和重连可能进一步延长耗时。这是特定测试条件下的观察结果，不是完成时间保证。

以 3,266,308 字节映像除以 2,700–3,000 秒，折算的平均有效速率约为 **1.09–1.21 kB/s**（十进制单位）。这只是按耗时计算的平均值，不是独立测得的无线链路基准。

当前实现将固件数据分块限制为 45 字节，并串行发送。每块使用带响应的 GATT 写入，随后等待匹配的 GAIA 确认，再发送下一块。已记录的传输探测使用 ATT MTU 100。协议头、两层确认、连接调度及主机和设备处理均会增加每个小块的开销。此停等设计是已知的应用层吞吐约束，但现有记录不能量化各因素分别占用的时间。

短程探测中，45 字节分块可以继续传输，55 字节分块出现停滞；停滞根因尚未确定。因此，45 字节是保守的实现选择，不能视为 BLE 的最大载荷。增加 MTU、无响应写入、流水线或其他传输方式，都需要单独验证协议兼容性和恢复行为；蓝牙标称物理层速率不能直接代表此升级通道的吞吐量。

相关实现见 [ResumableUpgrade](../../src/SilverMoon/Application/ResumableUpgrade.cs) 和 [WindowsBleTransport](../../src/SilverMoon/Infrastructure/WindowsBleTransport.cs)。耗时与分块探测属于项目测试记录，公开仓库未包含原始设备日志；这些结果不能推广为其他电脑、蓝牙适配器或音箱型号的性能保证。
