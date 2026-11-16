# 修改版安装、校验与断点恢复

本文说明 Middleton 已研究版本上的机制，不是任意型号、任意分区的通用刷写方法。它整理了项目早期实验与当前桌面实现，未发布原厂二进制或私人设备日志。

## 是否“跳过了固件校验”

准确说法是：修改版采用分区暂存与恢复状态流程，绕开正常完整包传输末尾的 RSA-PSS 验签入口；后续分区验证、复制、试启动、提交和读回仍存在。

正常更新完整接收 APPUPFTR 后校验分区内容。已修改的 r2 完整包在普通流程中曾返回 `003d`（OEM footer 验证失败）。此时升级上下文已清理，因此不是“先让验签失败，再强行继续”。

已验证的路径在所有镜像分区关闭、但尚未发送 APPUPFTR 时停止。升级恢复状态位于 PS key `0x0004`。严格核对暂存进度和映像身份后，只改已研究的 PRE_VALIDATE 恢复字段；普通重启后进入分区验证与 ImageUpgradeCopy 路径，而不重走前面的完整包 RSA 验签入口。

这没有取得厂商私钥、没有产生有效的新签名，也没有修改正在运行的升级库验签函数。它暴露的是升级恢复状态的信任边界；不能据此认定启动链没有其他校验，或任意固件都能启动。

## 当前实现保留哪些检查

1. **文件准入**：已知原版/r2 必须匹配完整 SHA-256。第三方文件必须满足固定布局、长度和受保护区间哈希，不能只改扩展名。
2. **精确分区边界**：完整文件为 3266308 字节；APPUPFTR 起始为 3266040。修改版传输停在该边界，还要核对设备随后请求 12 字节记录头及实际 PS4 状态。百分比达到 100% 不是证明。
3. **状态与身份**：核对型号、序列号、映像 ID、版本及恢复状态。保存 PS4 原内容，仅修改所需字段并完整读回；不能盲写整个上下文。
4. **后续验证与提交**：重启、恢复验证、试启动、提交、完成通知，以及重新连接后的预期版本/空闲状态读回共同决定成功。

官方回刷采用正常全量上传流程；修改版恢复路径与官方路径不可混为一谈。具体约束以源码为准，不建议手动照抄状态写入值。

## 为什么断线恢复不会只看本地进度条

BLE 的 ATT 写入成功、GAIA 命令 ACK、升级完成是不同层次。客户端逐帧等匹配的 ACK；重连后重新 SYNC 并核对设备状态，由设备的数据请求决定传输位置，而不是把 UI 百分比换算成文件偏移直接继续。

本地恢复记录按设备及文件哈希隔离；同一设备有排他锁。记录是辅助证据，不替代设备读回。累计 ACK 字节可包含重传，不能当作唯一字节覆盖量。超时、身份不符或协议不一致必须停下，而非把传输完成显示为安装成功。

文件 SHA-256 校验说明“我们准备的是哪一份文件”，不等于对设备内部每个写入字节进行了独立读回。最终仍依赖设备协议验证、提交结果和实际功能测试；这也是第三方代码兼容性无法由桌面程序保证的原因。

## 代码入口

- [FirmwareCatalog](../glass-playground/Domain/FirmwareCatalog.cs)、[第三方规范检查](../glass-playground/Domain/ThirdPartyFirmware.cs)：文件准入。
- [ResumableUpgrade](../glass-playground/Application/ResumableUpgrade.cs)：传输、恢复与提交状态机。
- [UpgradeContext](../glass-playground/Domain/UpgradeContext.cs)：PS4 解析及字段修改约束。
- [UpgradeWire](../glass-playground/Domain/UpgradeWire.cs)、[UpgradeMailbox](../glass-playground/Application/UpgradeMailbox.cs)：帧校验、ACK 与异步通知。
- [UpgradeJournalStore](../glass-playground/Infrastructure/UpgradeJournalStore.cs)：本地记录及设备锁。
- [恢复回归测试](../tests/DesktopReadOnly/ResumableUpgradeChecks.cs)：模拟断线与设备指定续传偏移；不能代替实机验证。
