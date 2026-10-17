## 2026-10-16 用户确认的集成范围
用户取消剩余 11 项（DR 8 项、Omni 3 项），不再将它们计为待完成或提供导入条目。排除名单为 plugin/tools/excluded-modules.json；保留原始源码用于追溯。
本次重新编译并逐项检查真实宿主类型及构造依赖：DR 303/303、Omni 53/53，共 356 项；离线生命周期、失败清理、配置、主题、来源、冲突和包完整性回归通过。
打包遇到任何保留模块编译失败或构造依赖缺失即停止，不以空提供器替代。不自动启用所有自动化模块。
真实游戏内逐项验证仍为 0；上述检查没有调用各上游模块构造函数或 Init，不能保证所有原生挂钩及外部服务运行成功。

以下为之前的源码审计记录，当前交付范围以上述记录为准。
# Omni 模块集成与来源标识

用户指定的 56 个模块均已定位对应公开源码，来源仓库、提交、路径和原始哈希见 local/omni-integration/source-manifest.json。移植后的独立模块源码在 local/omni-integration/upstream；当前主仓库和 Common 的完整快照分别保留在 local/omni-integration/repositories/YouShux、Common。

主仓库快照：0add07938310bce8e1bcb20b726adc4b2128dc3a。
Common 快照：0ff1d98fe2ccdce19ab2377546800eb8982138ea。
Fork 来源逐模块记录，优先使用当前主仓库实现，仅对主仓库没有的类回退到 Fork。不把公开模块中缺失的原宿主提供器视为已经完成。

模块页包含 311 个 DR 和 56 个 Omni 条目，导入后及候选条目均带来源后缀。当前通过编译 53 个 Omni，其中 53 个通过实际框架类型加载与构造依赖检查。当前没有编译通过但缺少构造提供器的模块，3 个编译待适配。当前可导入条目均默认停用；没有自动启用上游模块。

DR 与 Omni 同名或核心功能重复时不能同时启用；部分功能重叠仅展示提示，不删除任何来源。已筛选六组核心重复与六组部分重叠，见 docs/模块重复与重叠清单.md，并在模块页内提供同一清单。

配置注入使用构造参数的真实配置类型和保存回调；缺少取消、传送、库存等提供器时明确拒绝构造，不提供空实现。配置按模块完整类型名分开保存，不混用 DR/Omni 同名配置。普通 DrawSettings 返回变更、显式保存回调和模块停用/释放均可持久化构造配置。原生组件在首次明确导入 DR/Omni 模块时初始化，避免使用未初始化的 KamiToolKit。

通用 UI 使用保存的公开 Common 实现；主题令牌与字号映射 Yanjiu 当前主题，FF14 主题常规按钮使用 Yanjiu 同一原生灰色按钮绘制。标题使用用户提供的中文名称；通用本地化来自公开 Common，部分原宿主专用界面文本仍缺少公开本地化。不会把标题翻译补齐宣称为所有功能文本完成。

审计工具：plugin/tools/OmniCompatibilityAudit。
实际类型/提供器检查：plugin/tools/ModulePackageCheck。
打包：plugin/tools/pack-dr.ps1，Source 参数为 Omni；HostDirectory 可指定暂存构建目录。
最终构建：plugin/build.ps1，OutputDirectory 可指定 local/staging/plugin。
本地回归：plugin/tests/run-tests.ps1，ArtifactPath 可指向暂存构建。

源码及资源目前仅在用户本地集成；本次没有提交、推送或公开发布。重建工具、依赖源码及所需许可保留在对应目录。完整剩余错误见 local/omni-integration/兼容性进度.md。

2026-10-16 游戏日志确认宿主修复版于 09:32:55.103 完成加载，未重现旧 OmenTools 特征码错误。这仅验证宿主启动，逐模块实际游戏功能验证仍为 0。

2026-10-16 本轮补齐传送、最近水晶选路和实际库存观察服务。自动重建前往天穹街、城内自动行程通过 Lifestream 公开 IPC；缺少 Lifestream 会明确失败，不能视为完整独立替代。库存记录有界，暂不持久化不可见地点。新增选路、缓存与可选构造参数回归通过。
2026-10-16 09:59:37.532，当前更新包在实际游戏中完成宿主热加载；近期对应加载记录未显示 Yanjiu 错误。该记录仅证明宿主启动，不包含上游模块启用或功能验证。

自动使用未收藏物品的类别索引已独立适配，基于游戏物品、表情和发型表；动作类别值核对来源为 Dalamud/Game/UnlockState/ItemActionAction.cs：https://github.com/goatcorp/Dalamud/blob/c287045bf4400c4db66dfdc46598c229ab97a2dd/Dalamud/Game/UnlockState/ItemActionAction.cs 。公开来源池继续使用保存的 Common 实现。分类回归覆盖乐谱、肖像、表情与发型链接，以及排除无关解锁和秘籍误分类。
本轮补齐分解增强的真实技能提示服务；没有改动 Omen 启动扫描热修复库。
本轮物品查询已接入真实位置观察记录和提示设置；投影台、收藏柜与住宅仓库读取均使用当前 SDK 公开字段和状态，未读取的数据不作零数量断言。记录暂不持久化。详情见本地进度。
## 2026-10-16 指定 Omni 模块
重新加入用户指定且本地公开源码存在的四个 Omni 模块：BetterGlamourManagement、BetterUserMacro、MultiToolbar、RetainerEntrustMemory。它们保留 Omni 来源标记，并经过构造绑定检查后进入 Yanjiu 目录。
当前 Omni 公开源码目录和清单中没有“一键交互雇员铃”或独立“自动雇员”模块；RetainerEntrustMemory 是“雇员寄存记忆”，不是自动雇员全流程。未把不存在的源码名称伪造为模块。
## 2026-10-16 模块范围调整
用户取消 RetainerEntrustMemory（雇员寄存记忆），当前仅集成 BetterGlamourManagement、BetterUserMacro、MultiToolbar 三个 Omni 模块。
