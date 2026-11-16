# 官方固件获取、缓存与确定性修改

[English](../en/04-OFFICIAL-FIRMWARE-PREPARATION.md) · [文档目录](README.md)

固件选择页保留原交互和最终刷入确认。包含固件的本地完整包优先使用内置文件并验证精确哈希；不内置固件的便携包首次选择官方版或修改版时自动向 Marshall 获取 17.0.37，缓存原版，再按需在本地生成 r2。文件准备模块本身不发起 BLE 连接，也不开始刷入；界面此时可能已有设备连接。

## 信任边界

- 生产元数据接口：`https://fota.marshall.software/msdk/firmware`。不提交用户的序列号，使用历史接口所需的占位值。
- 仅接受 17.0.37、Lennox 的预期文件名、3266308 字节及代码内固定的完整 SHA-256。
- 下载仅限 HTTPS 官方主机和精确文件路径，拒绝跳转；不记录带签名的下载 URL。
- 本地补丁验证原版完整哈希、每处修改前字节，以及生成结果完整哈希。不会对未知版本进行模式搜索后自动修改。
- 缓存：`%LOCALAPPDATA%/MiddletonDesktop/firmware-cache/official-17.0.37.bin`。每次使用重新校验，写入通过临时文件原子替换；完整原版用于回刷。
- 已有有效缓存可离线使用。没有有效缓存时，官方返回新版或下载失效会拒绝准备，用户仍可导入符合已有规范的文件。
- 未来官方版本需独立分析、生成明确补丁和哈希、测试后更新项目。当前没有证明可查询任意历史版本的接口；不能保证官方长期保留 17.0.37。

## 验证

`tests/DesktopReadOnly` 默认包含准备模块的假 HTTP 测试：已知输入与 r2 逐字节比较、离线缓存、损坏缓存、未知版本、外域 URL、损坏/超长下载、取消。材料缺失的公开 CI 明确跳过材料测试。

显式联网检查（不执行 BLE 操作）：

```powershell
# 使用仓库 SDK 环境或本机 .NET 10
 dotnet run --project tests/DesktopReadOnly -- --firmware-download-check C:/your-test-cache
```

生成结果须匹配固定 r2 哈希。发布脚本检查完整性标签，避免继承 Low 标签影响配对或本地缓存访问。

## 输入身份与完整下载流程

| 项目 | 必需值 |
|---|---|
| 代号 | Lennox |
| 版本 | 17.0.37 |
| 文件名 | Lennox_FW_V17.0.37.bin |
| 长度 | 3266308 字节 |
| 官方 SHA-256 | db74c3468bd08d61f3a4e4e4e9e6eea9b19d95d6e852b9fd5e9cbac3e91c9472 |
| 修改版 SHA-256 | 80ed2088247976f10968f5d4aefb3cabf367ff0b0a1f774ac6e62a292c459024 |

[FirmwarePreparation](../../src/SilverMoon/Application/FirmwarePreparation.cs) 先受限读取缓存并核对完整哈希；无有效缓存时请求元数据，其中 serial_number 固定为占位值 x，不上传真实串号。元数据最多 65536 字节。下载 URL 要求 HTTPS、fota.marshall.software、默认端口、无 userinfo/fragment、路径恰为 /devices/Lennox/prod/Lennox_FW_V17.0.37.bin。允许服务端下载授权 query，但不记录该 URL。

生产 HttpClient 禁止重定向；若有 Content-Length 则检查，实际流也独立限制长度，最终检查精确大小与哈希。仅将验证后的原版写入临时文件并替换缓存，再返回原版或从副本生成 r2。准备流程有三分钟关联超时并响应取消；网络元数据不能新增版本或补丁指令。

版本字符串和官方主机名不能代替文件哈希。没有有效缓存且服务器不可用/返回未知版本时拒绝自动准备；当前机制不是任意历史版本选择器。下载成功只证明文件身份，不证明设备已安装。测试入口为 [FirmwarePreparationChecks](../../tests/DesktopReadOnly/FirmwarePreparationChecks.cs)。
