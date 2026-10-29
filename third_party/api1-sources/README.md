# API 1 对应源码与构建

本目录保存发行 DLL 的 AssemblyInformationalVersion 对应提交；详见 PROVENANCE.json。源码按原样归档，非从混淆宿主反编译得来。

## 重建布局

使用 .NET 10 SDK、Dalamud API 15（15.0.3.9 或兼容版本）和 Dalamud.CN.NET.Sdk 15.0.0。将以下目录复制到独立构建目录，按上游 ProjectReference 保持结构：

- OmniToolbox.Common/ 位于构建根目录。
- lib/OmenTools/、lib/KamiToolKit/、lib/TreeTools/、lib/TreeToolKit/、lib/FFXIVClientStructs.Expansion/ 使用本目录同名源码。
- OmniToolbox.Localizations/ 使用 Common 二进制嵌入的 xlf 资源快照（EmbeddedLocalizations/）。

随后 dotnet build OmniToolbox.Common/OmniToolbox.Common.csproj -c Release。上游 csproj 及 packages.lock.json 保留了 NuGet 依赖。SDK 的路径配置使用本机 Dalamud.CN.NET.Sdk 约定。预编译 DLL 的 SHA256 不应假定与本地重建逐字节相同；替换时重新生成 third_party/api1-runtime/SHA256.json 并运行 API 1 契约回归。

源码附带原许可证；未提供独立许可的上游目录不因归档而改标 GPL/MIT。宿主与其他第三方文件仍保留各自许可。
