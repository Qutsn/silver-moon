# UI 本地化架构与翻译规范

[English](../en/07-UI-LOCALIZATION.md) · [文档目录](README.md)

## 语言选择与显示

UI 当前仅支持中文和英文，采用系统显示语言选择和嵌入式资源加载。产品页面、设备/BLE 错误、升级阶段、恢复记录和第三方规范说明已迁移到中英资源；中英文目录须保持相同键集合；具体键数由校验脚本报告。研发材质实验窗口仍保留原文，不属于产品页面。

- 使用 .NET `CultureInfo.CurrentUICulture`，不根据时区、键盘、固件或设备名称判断。
- `zh-CN`、`zh-TW`、`zh-HK` 等归入 `zh`；当前共用简体中文资源。
- `en-*` 归入 `en`；其他语言以及不变区域归入 `en`。
- 当前语言没有键或值为空：回退 `zh`。两个资源都缺失才显示键名；发布前校验会阻止已引用键缺失。
- 启动时选定，改变 Windows 显示语言后重新打开程序。不实现运行中语言切换。
- 资源嵌入程序集；语言目录不从设备或固件加载，也不承载可执行翻译代码。

## 文件与 API

- `glass-playground/Domain/UiLanguage.cs`：语言父链匹配与回退，独立于 UI 框架。
- `glass-playground/Domain/AppText.cs`：不依赖 Avalonia 的共用资源加载与带参数文字；应用服务和基础设施可直接使用。
- `glass-playground/UiText.cs`：XAML 扩展、预览语言覆盖与 UI 入口。
- `glass-playground/Localization/zh.json`：中文原文。
- `glass-playground/Localization/en.json`：完整英文资源，保持键不变。
- `glass-playground/Localization/source-map.json`：最初提取的键与来源文件索引；后续语义键请使用代码搜索定位，索引不是完整性的权威依据。
- `glass-playground/check-localization.ps1`：检查重复键、键集合、空值、参数及实际 UI 引用。

XAML：`Text="{local:Tr ConnectionPreviewWindow.Text.10123}"`（示意键；实际键以资源为准），命名空间 `xmlns:local="using:GlassPlayground"`。
C#：`UiText.Get("实际键")`；参数：`UiText.Format("Upgrade.TransferredEvidence", acknowledgedBytes, skippedBytes)`。

初次提取的 `文件.Text.数字` 键中数字只是稳定标识，不是当前行号，不要随着代码移动重编号。新增文案推荐语义键，如 `Connection.ScanFailed`。可以逐组重命名既有键，但要同步所有语言和引用，不要只改英文一侧。

## 翻译步骤

1. 对照 `zh.json`，只填 `en.json` 的值；不要翻译键名、设备型号、固件版本、哈希、GAIA 标识和日志事件名。
2. 保留 `{0}`、`{1:N0}`、`{0:X8}` 等参数与格式，允许调整参数出现顺序。不要在代码中拼接英文语序。JSON 换行写成 `\n`。
3. 运行 `./glass-playground/check-localization.ps1`。空值、缺键、参数变化和未知资源引用均会失败。
4. 运行 `./tests/DesktopReadOnly/test.ps1` 和 `./glass-playground/build.ps1`。
5. 用 `SilverMoon.exe --preview --ui-culture=en-US` 预览英语选择；`zh-CN`、`zh-TW`、`de-DE` 分别验证中文归一与英语回退。该覆盖参数仅在 `--preview` 下有效，不改变 Windows 设置。演示模式不会执行实机刷入。
6. 检查较长英文在连接页、设备列表、详情页、风险确认、传输/中断/完成页的裁切、换行、按钮宽度和辅助功能名称；不能仅靠编译验证英语布局。

## 本地化边界和验证

- `Application/ResumableUpgrade.cs` 的阶段消息、Domain 的识别/结构说明以及 Infrastructure 的可见错误均使用 `AppText`。
- GAIA 命令、枚举状态、SHA-256、固件版本、安装规范标识以及设备原始名称保持原值；系统异常可能使用操作系统自己的语言。
- 研发预览窗口（MainWindow、NativeWindow、GlassPreviewWindow、ConceptWindow、材质调节等）不属于产品页面翻译范围。
- `tests/DesktopReadOnly/LocalizationChecks.cs` 验证语言归一、回退、完整资源和嵌入目录实际解析；测试只验证本地逻辑，不证明设备安装成功。
- 使用 `--preview --ui-culture=en-US --connection-smoke --localization-snapshots` 运行产品页面烟雾检查并记录连接、固件选择、详情、风险、耗时、传输和完成截图。截图写入诊断日志目录；可用 `MIDDLETON_LOG_DIRECTORY` 指定测试目录。`--discovery-smoke` 单独检查设备列表的行数、滚动和边框。
- 不使用预览截图作为真实设备行为的证明，不执行 BLE 写入或固件安装。

## 增加语言

创建同键集合资源文件（例如 `fr.json`），在 `UiLanguage.Supported` 中注册中性代码，在 csproj 的 `EmbeddedResource` 列表加入该文件，并扩展 `AppText` 的目录加载和选择分支（当前仅加载 zh/en，不能仅注册语言代码）。缺译先回退中文；若届时产品希望优先回退已完成英语，明确调整回退链并补测试。需要繁简分别翻译时，将资源注册和语言匹配扩展到具体文化，不能只往中文文件混入两套文案。
