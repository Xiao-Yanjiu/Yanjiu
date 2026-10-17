# 插件运行时依赖

这些 DLL 为运行时帮助库及模块编译器，不包含 OmniToolbox 或 DailyRoutines 宿主实现。版本和 SHA256 见 `SHA256.json`；原始版权与许可证见 `../../plugin/licenses`。

本地基线来自用户已安装的 OmniToolbox 1.1.6.6 发行包所附开源依赖，未经修改。源码来源：

| 库 | 许可证 | 对应源码 |
| --- | --- | --- |
| OmenTools | MIT | https://github.com/AtmoOmen/OmenTools/tree/c726d53456d56b82074a3ee8dafdef6ac1831811 |
| Roslyn | MIT | https://github.com/dotnet/roslyn/tree/35d9211b841e7613c1d2f8f5af6d628ace696c4c |
| AngleSharp 1.8.0 | MIT | https://github.com/AngleSharp/AngleSharp/tree/abc5527e2b0cb94954d260e386685bd1e5b537cb |
| GuerrillaNtp 3.1.0 | Apache-2.0 | https://github.com/robertvazan/guerrillantp |
| Markdig 0.43.0 | BSD-2-Clause | https://github.com/xoofx/markdig/tree/8c01cf054971a1ec4cd663edaca6e2d035236133 |
| MessageFormat 8.0.0 | MIT | https://github.com/jeffijoe/messageformat.net/tree/dae6f0b05094893f4f5e51ec46a0f0d68ba9ed54 |
| TinyPinyin 1.1.0 | MIT | https://github.com/hstarorg/TinyPinyin.Net |

各 NuGet 包的原始 nuspec 元数据同时保留，用于核对版本和授权。OmenTools 与 Roslyn 为带源码提交号的发行包版本；升级依赖时必须重新编译、运行兼容测试并更新哈希。

当前启动热修复已从公开源码重建 OmenTools，仅修改区域服务器地址解析；来源和修改记录见 docs/启动扫描错误热修复.md，完整源码位于 third_party/sources/OmenTools，通用重建入口为 plugin/tools/rebuild-omen.ps1。新构建库不是“未经修改”的原始发行包，运行 SHA256 仍以本目录清单为准。
