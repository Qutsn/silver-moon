# 桌面架构与代码审阅指南

[English](../en/06-CODE-REVIEWER-GUIDE.md) · [文档目录](README.md)

本指南面向希望复核实现的贡献者。代码注释使用简短英文，说明约束与原因；技术结论与证据边界在中英文文档中保持一致。构建脚本已支持公共 NuGet 和安装的 SDK；完整固件素材由使用者另行提供。

正式桌面源码位于 `src/SilverMoon/`，产品与可执行文件为 Silver Moon / `SilverMoon.exe`。内部 `GlassPlayground` 命名空间及工程文件名暂时保留，以维持现有代码和嵌入资源标识；它们不代表产品名称。

## 建议阅读顺序

| 要回答的问题 | 源码（相对仓库根目录） | 验证入口 |
|---|---|---|
| 用户改名后如何识别设备？ | `src/SilverMoon/Domain/DeviceProfiles.cs`、`DiscoveryCandidates.cs` | `tests/DesktopReadOnly/Program.cs` |
| 配对成功是否等于可访问？ | `Application/PairingNegotiation.cs`、`ProtectedGatt.cs`、`Infrastructure/WindowsBleTransport.cs`（均在 src/SilverMoon 下） | Program 中认证、通知订阅门控检查；docs/03-BLUETOOTH-AND-WINDOWS.md |
| 文件能否安装？ | `Domain/FirmwareContainer.cs`、`FirmwareCatalog.cs`、`Application/FirmwareImport.cs` | `FirmwareChecks.cs`；docs/05-THIRD-PARTY-FIRMWARE-GUIDE.md |
| 断线后从哪里续传？ | `Application/ResumableUpgrade.cs` 的 RunAsync/Transfer、`Domain/UpgradeContext.cs` | `ResumableUpgradeChecks.cs`、`UpgradeRecoveryChecks.cs`；docs/02-UPGRADE-AND-VALIDATION.md |
| ACK、通知、写入响应有什么区别？ | `Domain/UpgradeWire.cs`、`Application/UpgradeMailbox.cs` | `UpgradeChecks.cs`、`UpgradeRecoveryChecks.cs` |
| 如何防止两个进程同时刷入？ | `Infrastructure/UpgradeJournalStore.cs`、`UpgradeWorker.cs` | `JournalStoreChecks.cs`；实机进程/GATT 仍须验收 |
| 连续点击会覆盖另一条 EQ 吗？ | `Application/SoundWriteQueue.cs`、`SoundControl.cs`、`SoundStepperState.cs` | `SoundQueueChecks.cs`、`SoundStepperChecks.cs` |
| 旧页面/文字回调是否会污染新状态？ | `Views/ConnectionMotion.cs`、`TextMotion.cs`、`DeviceDiscovery.cs` | connection-smoke、discovery-smoke、sound-ui-smoke |

## 修改时必须保持的约束

1. 显示名称、型号、制造商和 GATT UUID 都能被外围设备伪造。当前检查是协议兼容性识别，不是 Marshall 厂商的密码学认证。BLE 绑定也不等于固件兼容性证明。
2. 已知文件 SHA-256 白名单与 middleton-mcu-v1 受保护区间检查决定当前可用安装策略。MD5 尾部只用于协议 image ID；仅容器结构检查通过不能授权未知文件，更不能保证任意第三方固件可运行。
3. 恢复记录只是本地证据；设备的 SYNC、PS4、数据请求必须重新核对。ConfirmedFrontier 不是下次直接 seek 的偏移；累计 ACK 可含重传，不能冒充唯一字节覆盖率。
4. 不把 GATT 写入成功当成 GAIA ACK，也不把 ACK 当成固件安装成功。完成仍需提交通知、断连观测及重新连接后的版本/空闲读回，不能证明开机音效已结束。
5. 一个 mailbox 只属于一个连接；ACK 超时或关联不明后丢弃该会话。worker 的 stdio 是私有串行协议，不是安全沙箱或公开 RPC 服务。
6. 设备实时值、待发送目标、界面显示模式是不同概念。过时请求的完成不能清除新请求目标；取消或退出不能顺便写入“恢复默认值”。
7. 涉及升级路径的改动先跑离线测试。假设备测试可验证程序逻辑，但不能替代实机安装、断线和回刷验收。

## 注释规范

- 在边界处解释输入信任级别、线程/会话所有者、取消及恢复语义；避免逐行翻译语法。
- 固件专用常量说明来源与适用范围。改变字节序、偏移、完成条件时同步修改测试和协议文档。
- 写清已经验证的事实与假设；不要写“绝对安全”“所有设备都支持”等无法证明的保证。
- 注释不应复制完整实现，避免代码改动后注释失效；公用流程用本指南链接到证据和测试。

## 当前复现方法与缺口

使用 `./tests/DesktopReadOnly/test.ps1` 和 `./src/SilverMoon/build.ps1`，两个脚本会退出当前 PowerShell 进程，应在独立进程运行。界面离线检查使用便携 EXE 的 `--preview --connection --discovery-smoke` / `--connection-smoke` / `--sound-ui-smoke` 参数，不连接真实音箱。

当前公开构建与交付约束：

- build.ps1 / test.ps1 优先使用安装的 SDK，支持显式指定固件路径；本地研究工具链仅作为开发回退。NuGet.Config 使用 nuget.org。
- 不带固件也能构建与运行部分离线测试；使用 WithoutFirmwareMaterials 时明确跳过真实文件依赖检查，不计为完整验证。固件导入仍按安装规范检查。
- 项目自有代码采用 GPL-3.0-only；第三方依赖 notices 随发布包保留，固件与依赖保留各自许可证。
- 公开技术文档按固件修改、升级校验、蓝牙实现排列；无固件构建不依赖未公开固件或本机日志；材料依赖测试仍须提供指定固件输入。请结合源码和随附测试复核结论。
## 公开资料边界

DeviceLog 当前只写本地，不自动上传；日志可能包含串号、蓝牙地址、本机路径和异常堆栈。公开 issue 前应脱敏；ETL、升级记录、截图、固件和研究归档应单独审核。凭据规则扫描只能识别所覆盖文件和格式中的匹配项；审查历史、二进制及日志需要分别进行，不能据此保证仓库不存在秘密。

静态审阅不能替代动态测试、依赖审计与实机验证，也不构成无漏洞保证。

## 可复现命令与文档维护

```powershell
pwsh -NoProfile -File ./tests/DesktopReadOnly/test.ps1 -WithoutFirmwareMaterials
pwsh -NoProfile -File ./src/SilverMoon/build.ps1 -WithoutBundledFirmware -CompressSingleFile -OutputDirectory publish-local
```

构建参数和完整素材测试见[构建说明](../../src/SilverMoon/RELEASE.md)。便携包自检使用 `SilverMoon.exe --portable-smoke <report-path>`，只检查资源、许可证和 worker 启动，不执行 BLE。各 UI 检查都须保留 `--preview --connection` 前缀。同编号中英文文档应同时更新，包括哈希、偏移、版本门控与不支持情形；详见[证据与验证](08-EVIDENCE-AND-VERIFICATION.md)。
