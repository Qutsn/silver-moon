# 技术文档

[English](../en/README.md) · [项目说明](../../README.zh-CN.md)

本文档描述已支持 Middleton 映像的机制、当前代码约束与证据边界，属于工程研究文档，不是通用 Marshall 规范。偏移和状态值不能直接推广到其他固件版本。

| 编号 | 文档 | 范围 |
|---|---|---|
| 01 | [休眠逻辑与固件修改](01-FIRMWARE-PATCHES.md) | MCU 地址空间、AUX/关机路径及完整 r2 差异 |
| 02 | [安装校验与断点恢复](02-UPGRADE-AND-VALIDATION.md) | RSA-PSS footer 路径、暂存恢复状态、PS4、续传与完成判据 |
| 03 | [蓝牙通道与 Windows](03-BLUETOOTH-AND-WINDOWS.md) | GATT/GAIA 协议、配对、声音控制与实测传输性能 |
| 04 | [固件获取与准备](04-OFFICIAL-FIRMWARE-PREPARATION.md) | 固定版本下载、网络约束、缓存与确定性补丁 |
| 05 | [第三方安装规范](05-THIRD-PARTY-FIRMWARE-GUIDE.md) | middleton-mcu-v1 字节范围与安装契约 |
| 06 | [架构与代码审阅](06-CODE-REVIEWER-GUIDE.md) | 源码/测试导读与不变量 |
| 07 | [UI 本地化](07-UI-LOCALIZATION.md) | 语言选择、资源目录、翻译与扩展 |
| 08 | [证据与验证方法](08-EVIDENCE-AND-VERIFICATION.md) | 复现、实机报告与结论边界 |

每篇提供对应语言链接。源码与测试采用仓库相对链接，随当前阅读版本变化；引用稳定内容时应使用具体提交。协议安装完成与固件功能正确不是同一个结论。
