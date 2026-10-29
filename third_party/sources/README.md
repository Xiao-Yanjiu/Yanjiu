# 本次发行的对应源码

- `DailyRoutines.ModulesPublic/`：公开模块快照 2748c84 的 Yanjiu 适配源码，AGPL-3.0；原始来源及哈希见 DR-source-manifest.json。311 个目录项，303 个可编译，8 个待适配。修改主要是宿主 API、TaskHelper 事件和独立构建条件适配。
- `KamiToolKit/`、`OmenTools.KamiToolKit/`：所附原生 UI 运行库的公开源码，保留 MIT LICENSE。
- `OmenTools/`：c726d53456d56b82074a3ee8dafdef6ac1831811 的修改版完整源码，MIT；区域服务器扫描修复见 docs/启动扫描错误热修复.md。
- `OmniHostResources/`：兼容宿主编译使用的公开数据资源及模块名称。来源 YouShux/OmniToolbox.Common，快照 0ff1d98fe2ccdce19ab2377546800eb8982138ea。实际使用的兼容代码位于 plugin/Yanjiu/Compatibility/OmniPublic/，保留上游命名与来源。本发行不打包 Omni 功能模块或 Omni 宿主 DLL。
- `DRLocalizations/`：DailyRoutines.Localizations 中文语言资源，保留来源归属，不将代码许可证扩张为资源的独立授权。

其他运行库版本、许可证和来源见 third_party/plugin-runtime/README.md、plugin/licenses/ 和 third_party/licenses/。模块与第三方部分各自保留原许可证；包含 AGPL 模块的发行须遵守其 AGPL 条款。源码可从同一 Release 下载或通过对应 Git 标签取得。

新版 API 1 运行库对应源码单独位于 third_party/api1-sources/，与旧 DR 的 OmenTools 运行库隔离；准确提交及构建来源见其中 PROVENANCE.json。
