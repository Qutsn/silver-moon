# Silvered Moon

Unofficial Marshall Middleton management and firmware utility. Install modified firmware to disable AUX sleep and ten-minute auto-off.

[Download](https://github.com/Qutsn/silvered-moon/releases) · [中文](#中文)

## Features

- Bluetooth LE discovery, pairing, device information, and volume/EQ controls.
- Official 17.0.37 download, SHA-256 verification, local patching, and rollback.
- Firmware installation with reconnection and resume support.
- Third-party firmware import restricted to the supported format.
- Portable Windows x64 EXE with English and Chinese UI.

## Usage

Download `SilveredMoon.exe` from Releases. Requires Windows 10 2004 / build 19041 or later; no separate .NET installation. Turn on the speaker, select its LE device, then open controls or choose firmware. Initial firmware preparation requires internet access; verified cache can be reused offline.

Pre-release, unsigned EXE. Windows may display SmartScreen warnings. `SHA256SUMS.txt` contains the EXE checksum for verifying download integrity; it is not a digital signature.

## Technical Documentation

Research documents are currently in Chinese.

1. [Sleep logic and firmware patches](docs/01-FIRMWARE-PATCHES.md)
2. [Upgrade validation and resume](docs/02-UPGRADE-AND-VALIDATION.md)
3. [Bluetooth and Windows](docs/03-BLUETOOTH-AND-WINDOWS.md)
4. [Firmware download and preparation](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md)
5. [Third-party firmware format](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md)

Supports Middleton official 17.0.37 and project r2 (display version 25.2.52). The patch disables the identified AUX sleep call and ten-minute shutdown request, not all power protections. Third-party format validation does not guarantee functional firmware.

## Build

Windows, PowerShell 7, and .NET SDK 10:

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

Tests requiring firmware materials are skipped in this mode. [Build guide](glass-playground/RELEASE.md) · [Code guide](docs/06-CODE-REVIEWER-GUIDE.md) · [Localization](docs/07-UI-LOCALIZATION.md) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

## License

Project-owned code is licensed under [GPL-3.0-only](LICENSE). Distributed derivative works must comply with its source-code and licensing requirements. Firmware and third-party dependencies retain their respective licenses. This project is not affiliated with or endorsed by Marshall.

---
# 中文

非官方 Marshall Middleton 管理与固件工具。刷入修改版固件，关闭 AUX 休眠与十分钟自动关机。

[下载](https://github.com/Qutsn/silvered-moon/releases) · [English](#silvered-moon)

## 功能

- 蓝牙 LE 扫描、配对、设备信息与音量/EQ 控制。
- 官方 17.0.37 下载、SHA-256 校验、本地修改与回刷。
- 固件安装、断线重连与续传。
- 导入符合规范的第三方固件。
- Windows x64 单文件便携程序，中英文界面。

## 使用

从 Releases 下载 `SilveredMoon.exe`。支持 Windows 10 2004 / build 19041 及以上，无需另装 .NET。打开音箱，选择 LE 设备，即可控制声音或选择固件。首次准备固件需要联网，已验证缓存可离线复用。

当前为预发布版本，EXE 未签名，Windows 可能显示 SmartScreen 提示。`SHA256SUMS.txt` 是 EXE 的校验清单，用于验证下载完整性，不是数字签名。

## 技术文档

1. [休眠逻辑与固件修改](docs/01-FIRMWARE-PATCHES.md)
2. [升级校验与断点续传](docs/02-UPGRADE-AND-VALIDATION.md)
3. [蓝牙通道与 Windows 实现](docs/03-BLUETOOTH-AND-WINDOWS.md)
4. [固件下载与准备](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md)
5. [第三方固件规范](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md)

支持 Middleton 官方 17.0.37 与项目 r2（显示版本 25.2.52）。补丁关闭已定位的 AUX 休眠调用及十分钟关机请求，不是全部电源保护。第三方文件通过规范检查不代表固件功能正常。

## 构建

需要 Windows、PowerShell 7 和 .NET SDK 10，使用上方英文部分的构建命令。此模式跳过依赖固件素材的测试。

[构建指南](glass-playground/RELEASE.md) · [代码导读](docs/06-CODE-REVIEWER-GUIDE.md) · [本地化](docs/07-UI-LOCALIZATION.md) · [参与贡献](CONTRIBUTING.md) · [安全问题](SECURITY.md)

## 许可证 License

项目自有代码采用 [GPL-3.0-only](LICENSE)。分发衍生作品时须遵守其源码提供及许可证要求。固件与第三方依赖保留各自许可证。本项目与 Marshall 无隶属或背书关系。
