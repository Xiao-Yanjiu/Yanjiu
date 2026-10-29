# API 1 隔离运行库

本目录使用 Omni 1.1.8.7 中公开共享运行库的原始 DLL。没有打包 OmniToolbox.dll、私有 Tree 或 TreePublic 主体。

文件摘要：SHA256.json。每个自定义运行库的 AssemblyInformationalVersion 中记录了源码提交；对应完整快照位于 ../api1-sources/，溯源清单为 ../api1-sources/PROVENANCE.json。

OmenTools、KamiToolKit、FFXIVClientStructs.Expansion 的原许可证随源码保留。Common 的数据资源保留原来源和许可。YouShux 的 Common、TreeTools、TreeToolKit 对应快照没有独立 LICENSE 文件：本仓库不为这些上游文件附加或虚构授权，来源和作者归属保持不变。

NuGet 依赖的包版本来自 OmniToolbox.deps.json，清单和许可信息见 ../api1-sources/NUGET-PROVENANCE.json 及 ../licenses/。构建方法见 ../api1-sources/README.md。
