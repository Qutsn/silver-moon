# Silvered Moon

非官方 Marshall Middleton 管理与固件工具。刷入修改版固件，关闭 AUX 休眠与十分钟自动关机。

[English](README.md) · [下载](https://github.com/Qutsn/silvered-moon/releases)

## 功能

- 蓝牙 LE 扫描、配对与设备信息读取。
- 音量、低音、高音调节及设备状态同步。
- 官方固件下载、SHA-256 校验、本地修改与原版缓存回刷。
- 固件安装、断线重连与续传。
- 导入符合[安装规范](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md)的第三方固件。
- 单文件便携 EXE，自动选择中英文界面。

## 使用

从 [Releases](https://github.com/Qutsn/silvered-moon/releases) 下载 `SilveredMoon.exe`。支持 Windows 10 2004 / build 19041 及以上的 x64 系统，无需另装 .NET。

打开音箱，扫描并选择 LE 设备，即可进入声音控制或固件选择。首次准备固件需要联网，已验证的缓存可离线复用。

**预发布版本 · EXE 未签名。** Windows 可能显示 SmartScreen 提示；发布附件包含 SHA-256 校验值。

## 技术文档

| 主题 | 文档 |
|---|---|
| 休眠条件、MCU 补丁与 r2 的 18 字节差异 | [固件修改](docs/01-FIRMWARE-PATCHES.md) |
| 升级恢复状态、校验机制与断点续传 | [安装与校验](docs/02-UPGRADE-AND-VALIDATION.md) |
| 音频与 BLE、GATT 服务、Windows 配对及声音控制 | [蓝牙与 Windows](docs/03-BLUETOOTH-AND-WINDOWS.md) |
| 版本检查、下载、缓存与本地补丁生成 | [固件准备](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md) |

当前支持 Middleton 官方 17.0.37 与项目 r2（显示版本 25.2.52）。补丁移除的是已定位的 AUX 休眠调用与十分钟关机请求，不是所有电源管理或保护机制。安装路径避开正常完整包的验签入口，但没有生成有效厂商签名，也没有取消全部后续检查。

第三方文件通过规范检查不代表固件功能正常。测试状态与限制见发布说明。

## 构建

需要 Windows、PowerShell 7 与 .NET SDK 10：

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

此模式跳过依赖原厂固件素材的测试。

[构建指南](glass-playground/RELEASE.md) · [代码导读](docs/06-CODE-REVIEWER-GUIDE.md) · [本地化](docs/07-UI-LOCALIZATION.md) · [参与贡献](CONTRIBUTING.md) · [安全问题](SECURITY.md)

项目自有代码采用 [GPL-3.0-only](LICENSE)。与 Marshall 无隶属或背书关系。固件及依赖保留各自权利；接口可访问不等于取得第三方使用许可。


