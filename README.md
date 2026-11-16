# Silvered Moon

Unofficial Marshall Middleton management and firmware utility. Install modified firmware to disable AUX sleep and ten-minute auto-off.

[简体中文](README.zh-CN.md) · [Download](https://github.com/Qutsn/silvered-moon/releases)

## Features

- Bluetooth LE discovery, pairing, and device information.
- Volume, bass, and treble controls with device-state updates.
- Official firmware download, SHA-256 verification, local patching, and cached rollback files.
- Firmware installation with reconnection and resume support.
- Import of third-party firmware matching the [supported format](docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md).
- Portable EXE with automatic English/Chinese language selection.

## Usage

Download `SilveredMoon.exe` from [Releases](https://github.com/Qutsn/silvered-moon/releases). Requires Windows 10 2004 / build 19041 or later, x64; no separate .NET installation needed.

Turn on the speaker, scan and select its LE device, then open controls or choose firmware. Initial firmware preparation requires internet access; verified cached files can be reused offline.

**Pre-release · unsigned EXE.** Windows may display a SmartScreen warning. Release assets include SHA-256 checksums.

## Technical Documentation

The linked research documents are currently in Chinese.

| Topic | Documentation |
|---|---|
| Sleep conditions, MCU patches, and the 18-byte r2 change | [Firmware modifications](docs/01-FIRMWARE-PATCHES.md) |
| Upgrade recovery states, verification, and resume behavior | [Installation and validation](docs/02-UPGRADE-AND-VALIDATION.md) |
| Audio vs. BLE, GATT services, Windows pairing, and sound controls | [Bluetooth and Windows](docs/03-BLUETOOTH-AND-WINDOWS.md) |
| Supported-version checks, downloads, caching, and local patching | [Firmware preparation](docs/04-OFFICIAL-FIRMWARE-PREPARATION.md) |

Currently supports Middleton with official 17.0.37 and project r2 (display version 25.2.52). The patch suppresses the identified AUX sleep call and ten-minute shutdown request—not all power-management or protection mechanisms. The installation path avoids the normal full-package signature-verification entry point; it neither creates a valid vendor signature nor removes all subsequent checks.

Third-party admission does not guarantee working firmware. See release notes for testing status and limitations.

## Build

Windows, PowerShell 7, and .NET SDK 10:

```powershell
./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
./glass-playground/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

Tests requiring proprietary firmware are skipped in this mode.

[Build guide](glass-playground/RELEASE.md) · [Code guide](docs/06-CODE-REVIEWER-GUIDE.md) · [Localization](docs/07-UI-LOCALIZATION.md) · [Contributing](CONTRIBUTING.md) · [Security](SECURITY.md)

[GPL-3.0-only](LICENSE) for project-owned code. Not affiliated with or endorsed by Marshall. Firmware and dependencies retain their respective rights; API availability does not establish permission for third-party use.


