# Silver Moon

Unofficial Marshall Middleton management and firmware utility. Disable AUX sleep and ten-minute auto-off with an automated firmware workflow.

[Download](https://github.com/Qutsn/silver-moon/releases) · [中文](#中文)

## Why Silver Moon

Why can't a speaker's owner choose when AUX goes to sleep or when the speaker turns itself off? Middleton's stock firmware makes those choices for you, with no way to change them. Silver Moon began with that frustration: people who own a device should have a say in how it works.

The current patch disables AUX sleep and ten-minute auto-off. Custom timeout settings are not yet supported.

## Features

- **Automated firmware preparation:** download the supported official firmware, verify its SHA-256, and generate the modified firmware locally.
- **Guided installation:** select a speaker and firmware, confirm installation, then let the app handle transfer, reconnection, resume, and completion checks.
- **Official firmware rollback:** restore the original sleep and auto-off behavior.
- **Everyday controls:** device information, volume, and EQ over Bluetooth LE.
- **Portable Windows app:** a single EXE with English and Chinese UI, plus import of third-party firmware that meets the supported format.

## How it works

1. **Connect.** Open `SilverMoon.exe`, turn on the speaker, and select its LE device. The app handles pairing and checks device compatibility.
2. **Choose.** Select modified firmware to disable AUX sleep and auto-off, or official firmware to restore them. The app downloads and verifies the supported base firmware and prepares the selected image automatically.
3. **Install.** Confirm installation. The app transfers the firmware, attempts reconnection after interruptions, and resumes according to the speaker's reported state.
4. **Finish.** After the upgrade and restart, the app reconnects and checks the device state before reporting completion.

No manual firmware patching or command-line flashing is needed for the built-in choices. Initial firmware preparation requires internet access; verified cached files can be reused offline.

Windows 10 2004 / build 19041 or later, x64. This is an unsigned pre-release; Windows may display SmartScreen warnings. `SHA256SUMS.txt` lets you verify the EXE download's integrity.

## Technical Documentation

Research documents are currently in Chinese.

1. [Sleep logic and firmware patches](docs/01-FIRMWARE-PATCHES.md)
2. [Upgrade validation and resume](docs/02-UPGRADE-AND-VALIDATION.md)
3. [Bluetooth and Windows](docs/03-BLUETOOTH-AND-WINDOWS.md)
4. [Firmware download and preparation](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md)
5. [Third-party firmware format](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md)

Supports Middleton official 17.0.37 and project r2 (display version 25.2.52). The patch does not disable all power protections. Third-party format validation does not guarantee working firmware.

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

非官方 Marshall Middleton 管理与固件工具。通过自动化固件安装流程，关闭 AUX 休眠与十分钟自动关机。

[下载](https://github.com/Qutsn/silver-moon/releases) · [English](#silver-moon)

## 项目初衷

音箱为什么不能让用户自行设置 AUX 的休眠时间和自动关机时间？Middleton 的官方固件替用户做出了这些选择，却没有留下修改的入口，无论这些设置是否符合个人习惯。Silver Moon 最初就来自这份不满：我们希望把选择权交还给拥有设备的人。

当前补丁实现了关闭 AUX 休眠和十分钟自动关机，尚不支持自定义时间。

## 功能亮点

- **自动准备固件：**下载支持的官方固件、校验 SHA-256，并在本地生成修改版。
- **引导式安装：**选择音箱与固件、确认安装后，由软件处理传输、断线重连、续传与完成检查。
- **回刷官方固件：**恢复原有的休眠与自动关机行为。
- **日常控制：**通过蓝牙 LE 查看设备信息、调节音量与 EQ。
- **便携桌面程序：**Windows 单文件 EXE，中英文界面，也可导入符合规范的第三方固件。

## 使用流程

1. **连接音箱。**打开 `SilverMoon.exe`，开启音箱并选择其 LE 设备，软件处理配对并检查设备兼容性。
2. **选择固件。**选择修改版以关闭 AUX 休眠与自动关机，或选择官方版以恢复原有行为。软件自动下载、校验支持的基础固件，并准备所选固件。
3. **确认安装。**软件开始传输；连接中断时尝试重连，根据音箱报告的状态恢复传输。
4. **等待完成。**升级与重启后，软件重新连接并检查设备状态，再报告安装完成。

使用内置选项时，不需要手动修改固件或通过命令行刷入。首次准备固件需要联网，已验证的缓存可离线复用。

支持 Windows 10 2004 / build 19041 及以上的 x64 系统。当前为未签名的预发布版本，Windows 可能显示 SmartScreen 提示。`SHA256SUMS.txt` 用于检查 EXE 下载是否完整。

## 技术文档

1. [休眠逻辑与固件修改](docs/01-FIRMWARE-PATCHES.md)
2. [升级校验与断点续传](docs/02-UPGRADE-AND-VALIDATION.md)
3. [蓝牙通道与 Windows 实现](docs/03-BLUETOOTH-AND-WINDOWS.md)
4. [固件下载与准备](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md)
5. [第三方固件规范](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md)

支持 Middleton 官方 17.0.37 与项目 r2（显示版本 25.2.52）。补丁不会关闭全部电源保护机制。第三方文件通过规范检查不代表固件功能正常。

## 构建

需要 Windows、PowerShell 7 和 .NET SDK 10，使用上方英文部分的构建命令。此模式跳过依赖固件素材的测试。

[构建指南](glass-playground/RELEASE.md) · [代码导读](docs/06-CODE-REVIEWER-GUIDE.md) · [本地化](docs/07-UI-LOCALIZATION.md) · [参与贡献](CONTRIBUTING.md) · [安全问题](SECURITY.md)

## 许可证 License

项目自有代码采用 [GPL-3.0-only](LICENSE)。分发衍生作品时须遵守其源码提供及许可证要求。固件与第三方依赖保留各自许可证。本项目与 Marshall 无隶属或背书关系。
