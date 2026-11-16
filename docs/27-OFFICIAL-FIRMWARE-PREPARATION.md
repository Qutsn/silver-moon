# 官方固件按需准备（2026-11-16）

固件选择页保留原交互和最终刷入确认。包含固件的本地完整包优先使用内置文件并验证精确哈希；不内置固件的便携包首次选择官方版或修改版时自动向 Marshall 获取 17.0.37，缓存原版，再按需在本地生成 r2。准备文件不连接音箱、不开始刷入。

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

本次真实生产下载成功，生成 r2 哈希与已验收文件一致。开发目录启动的进程受现有 Low 完整性标签影响时可能无法写 LocalAppData；应通过 stage-release.ps1 的正式固定入口运行，不修改系统蓝牙权限。

## 公开发行与其他项目

- [NikonFirmwarePatch](https://github.com/XinmiaoZhao/NikonFirmwarePatch)：原厂固件由用户从 Nikon 获得，仓库不包含原厂固件，只接受指定 C 3.11，拒绝 C 3.12；旧说明页下线后仍引用 Nikon 托管的存档链接并核验哈希。
- [ReVanced Manager](https://github.com/ReVanced/revanced-manager)：提供本地补丁及下载器机制。不能把其存在理解为任何第三方下载源或接口已获授权。

这些是分发模式的参考，不是法律先例或授权证明。不托管完整官方/修改固件能减少本项目直接再分发的行为，但未解决 Marshall 接口使用和修改固件的全部许可问题。公开发行决定仍需单独处理；本次没有上传固件、发布 GitHub Release 或联系厂商。
