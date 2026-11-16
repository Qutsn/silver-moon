# Silvered Moon

**让声音继续。** 一个非官方的 Marshall Middleton Windows 管理与固件工具。

修改版固件去掉官方固件中恼人的 AUX 频繁休眠和自动关机功能——具体移除的是已定位的 **AUX 功放休眠调用与十分钟自动关机请求**，并非关闭所有电源保护。

[下载便携版](https://github.com/Qutsn/silvered-moon/releases) · [固件修改原理](docs/28-FIRMWARE-PATCHES.md) · [安装与校验机制](docs/29-UPGRADE-AND-VALIDATION.md) · [蓝牙与 Windows 连接](docs/30-BLUETOOTH-AND-WINDOWS.md)

## 可以做什么

- **管理音箱**：发现并连接 BLE 设备，识别 Middleton，查看设备信息、电量及固件版本。
- **调节声音**：调整音量、低音和高音，并接收音箱实体操作后的状态更新。
- **安装修改版或回刷官方版**：首次按需从官方服务器下载已支持的 17.0.37，核验完整哈希，在本机生成修改版；保留校验后的原版缓存。
- **断线恢复**：重新连接后核对音箱升级状态，由设备请求决定继续传输的位置。
- **导入第三方固件**：只接受符合 [middleton-mcu-v1 规范](docs/19-THIRD-PARTY-FIRMWARE-GUIDE.md)的文件，不支持任意二进制。
- **便携与双语**：单文件 EXE，无需安装 .NET；中文系统显示中文，其余系统显示英文。

## 开始使用

1. 从 [Releases](https://github.com/Qutsn/silvered-moon/releases) 下载 `SilveredMoon.exe`，在 Windows 10 2004 / build 19041 及以上的 x64 电脑运行。
2. 打开音箱，在软件中扫描并选择设备；首次连接按需完成 LE 配对。
3. 进入详情页控制声音，或选择固件、阅读确认信息后安装。首次准备固件需要联网，有效缓存可离线复用。

当前为 **Pre-release，EXE 未签名**，Windows 可能显示未知发布者或 SmartScreen 提示。发布页附有 SHA-256 校验文件。

## 关于 Middleton，我们发现了什么

| 主题 | 研究结论与实现 |
|---|---|
| 为什么 AUX 会休眠？ | MCU 根据模式、计数、输入报告和 GPIO 等条件进入功放休眠；低输入判定与唤醒判定有不同门槛。[休眠逻辑与补丁](docs/28-FIRMWARE-PATCHES.md) |
| 修改版具体改了多少？ | 两个 MCU 变体中的休眠调用、十分钟关机请求，以及 QCC 的版本显示标记；完整文件实际差异为 18 字节。[地址、原字节与边界](docs/28-FIRMWARE-PATCHES.md#r2-的全部补丁) |
| 没有厂商私钥，为什么能安装？ | 此版本可通过升级恢复状态进入另一条验证/复制路径，避开正常完整包尾部 RSA-PSS 验签入口；不是伪造签名，也不是跳过所有验证。[安装与校验机制](docs/29-UPGRADE-AND-VALIDATION.md) |
| 为什么有音频设备和 LE 设备？ | 音频播放与 BLE 管理/升级是不同通道；LE 名称不应作为唯一型号判断。[蓝牙通道与服务](docs/30-BLUETOOTH-AND-WINDOWS.md) |
| Windows 配对与重连有什么坑？ | 配对、受保护 GATT 访问、进程完整性标签、重启后的会话生命周期需要分别处理。[Windows 实现](docs/30-BLUETOOTH-AND-WINDOWS.md#windows-端如何连接) |
| 音量为何有时读到 0？ | 尚未播放时设备可能上报 0；开始播放或操作实体按键后才同步，官方 App 也观察到类似行为。[声音控制与读回](docs/30-BLUETOOTH-AND-WINDOWS.md#声音控制与读回) |
| 官方以后更新怎么办？ | 仅处理精确匹配的已支持版本，未知版本拒绝自动修改，待项目重新适配。[下载、缓存与本地生成](docs/27-OFFICIAL-FIRMWARE-PREPARATION.md) |

这些结论来自特定 Middleton 固件的分析、协议实验和用户验收，不是所有 Marshall 设备的通用规范。原始固件、反编译材料和含设备标识的本机日志不随仓库分发。

## 支持范围

当前针对 Middleton、官方 17.0.37 与项目修改版 r2（显示版本 25.2.52）。用户已验收 AUX/十分钟关机修改，以及回刷官方后休眠恢复。第三方固件实机验收、朋友电脑修复后复测及最新下载版的完整跨机验收暂缓，具体限制见 Release 说明。结构校验通过不保证第三方 MCU 代码可启动或功能正确。

## 构建与贡献

Windows + PowerShell 7 + .NET SDK 10：

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

材料相关测试会跳过，不代表完整固件验证。[构建说明](glass-playground/RELEASE.md) · [代码导读](docs/22-CODE-REVIEWER-GUIDE.md) · [翻译指南](docs/20-UI-LOCALIZATION.md) · [参与贡献](CONTRIBUTING.md) · [安全问题](SECURITY.md)

项目自有代码采用 [GPL-3.0-only](LICENSE)。与 Marshall 无隶属或背书关系；固件及依赖保留各自权利，接口可访问不等于获得第三方使用授权。
