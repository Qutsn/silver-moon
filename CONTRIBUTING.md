# Contributing

Project-owned code is licensed under GPL-3.0-only; see [LICENSE](LICENSE). Firmware and third-party dependencies do not automatically inherit this license. Contributors must have the right to submit their work under the project license.

Read the [code review guide](docs/en/06-CODE-REVIEWER-GUIDE.md), [third-party installation profile](docs/en/05-THIRD-PARTY-FIRMWARE-GUIDE.md) and [localization guide](docs/en/07-UI-LOCALIZATION.md).

- Use Windows, PowerShell 7 and .NET SDK 10; see the [build instructions](glass-playground/RELEASE.md).
- Without firmware materials, run `./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials`. It explicitly skips material-dependent checks and is not the full test suite.
- Full material-backed tests require the exact official/r2 hashes. Do not attach firmware or personal diagnostic logs to ordinary pull requests.
- Protocol changes must preserve failure-stop behavior, identity checks and post-restart readback, with offline regressions. Report physical test conditions/results separately from simulations.
- Comments should explain rationale, boundaries and evidence rather than restating syntax. Update both documentation languages and tests when constraints change.
- Issues should state version, OS, reproduction and expected behavior. Redact serials, Bluetooth addresses, usernames and local paths from logs.

---
# 参与开发与审阅

本项目自有代码采用 GPL-3.0-only，见 LICENSE。第三方依赖与固件不自动适用本项目许可证。提交贡献应由贡献者拥有相应权利，并同意按该许可证提供。

请先阅读[代码审阅指南](docs/zh-CN/06-CODE-REVIEWER-GUIDE.md)和[第三方安装规范](docs/zh-CN/05-THIRD-PARTY-FIRMWARE-GUIDE.md)。界面翻译见[本地化指南](docs/zh-CN/07-UI-LOCALIZATION.md)。

- 使用 Windows、PowerShell 7 和 .NET SDK 10；具体构建参数见[构建说明](glass-playground/RELEASE.md)。
- 没有合法取得的固件素材时，可运行 `./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials`；这会明确跳过真实固件相关测试，不能当作全套验证。
- 完整测试需提供指定哈希的官方/r2 文件；不要把固件或本机诊断日志作为普通 PR 附件。
- 改动升级协议时必须保留检查失败即停止的行为、身份核对及重启后读回，并提供离线回归。实机测试应单独记录设备前提和结果，不把模拟结果描述为实机成功。
- 注释解释原因、边界和证据，不逐行复述语法。修改约束时同步更新文档与测试。
- Issue 中先描述版本、系统、复现步骤和预期结果。日志中的串号、蓝牙地址、用户名和路径应脱敏。

