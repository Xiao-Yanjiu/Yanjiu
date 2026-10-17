# 当前状态：2026-10-16，DR 专用版
用户已撤回保留 Omni 的要求：12 组全部保留 DR，移除所有 Omni 模块、自动恢复配置条目和模块列表。
DR 全量 311 项重新纳入目录，其中 303 项通过程序集绑定并自动加载；8 项仍因原宿主接口缺失未能实现，明确标为待适配，不可启用。恢复目录不等于恢复功能。
所有 DR 卡片常显中文说明、前置模块、双向冲突模块与可搭配模块；模块尚未构造时使用源码提取的关系。动态本地化说明由模块加载后填充。
分类改为左侧竖排，右侧只保留全部/已启用/收藏；标题占用弹性列、说明自动换行，移除空作者占位。
首次更新清除旧颜色覆盖，恢复 FF14 原生主题（深灰、暖金边框、米白字）。后续可自行编辑配色。
离线回归通过：311项说明与关系、303项类型绑定、内存加载目录、生命周期、递归依赖、冲突、配置与主题。
以下为历史记录，范围以此段为准。
## 2026-10-16 用户确认的集成范围
用户取消剩余 11 项（DR 8 项、Omni 3 项），不再将它们计为待完成或提供导入条目。排除名单为 plugin/tools/excluded-modules.json；保留原始源码用于追溯。
本次重新编译并逐项检查真实宿主类型及构造依赖：DR 303/303、Omni 53/53，共 356 项；离线生命周期、失败清理、配置、主题、来源、冲突和包完整性回归通过。
打包遇到任何保留模块编译失败或构造依赖缺失即停止，不以空提供器替代。不自动启用所有自动化模块。
真实游戏内逐项验证仍为 0；上述检查没有调用各上游模块构造函数或 Init，不能保证所有原生挂钩及外部服务运行成功。

以下为之前的源码审计记录，当前交付范围以上述记录为准。
# Daily Routines 本地集成

当前是进行中的开发版，不属于“全部兼容”。311 个模块已加入框架目录，其中 303 个通过编译和框架类型加载，8 个标记待适配。真实游戏内验证为 0。逐项错误和范围见 local/dr-integration/兼容性进度.md 与 local/dr-compatibility-integrated/compatibility.json。

导入 local/artifacts/plugin/Yanjiu.dll，保留同目录依赖、Assets 和 DR 文件夹。在模块页可搜索 Daily Routines 模块，按需导入后手动启用，默认停用。模块启用检查实际前置模块、双向冲突和客户端区域；移除 DR 原宿主账户/Tester 门槛。模块包记录宿主和每个 DLL 的 SHA256，不匹配会拒绝导入。

303 个模块的类型加载测试没有运行构造函数或 Init；它证明程序集能被宿主识别，不能证明游戏内挂钩、原生窗口、自动化及外部服务全部可用。当前没有自动启用全部模块。

## 来源与许可

- DailyRoutines.ModulesPublic：https://github.com/Dalamud-DailyRoutines/DailyRoutines.ModulesPublic ，快照 2748c84，AGPL-3.0。源码、原始哈希和许可保留在 local/dr-integration/upstream 及 source-manifest.json。
- 公共接口：https://github.com/Dalamud-DailyRoutines/DailyRoutines.Common 。缺失的原宿主实现没有冒用其他闭源 DLL。
- KamiToolKit：https://github.com/Dalamud-DailyRoutines/KamiToolKit ，MIT。
- OmenTools.KamiToolKit：https://github.com/AtmoOmen/OmenTools.KamiToolKit ，MIT。
- 两个原生 UI 依赖的完整公开源码保留在 local/dr-integration/dependencies，MIT 许可同时复制到 third_party/licenses 和插件输出；运行库校验值在 third_party/plugin-runtime/SHA256.json。
- 中文文本来自 DailyRoutines.Localizations，仅作为本地语言资源，未进行公共发布。

本地移植修改包含 TaskHelper 事件名适配、去除原宿主账户条件、将仅 DEBUG 的独立模块纳入本地构建。原始 source-manifest.json 的源码哈希用于追溯原始快照，不代表移植后文件的哈希。运行 DLL 使用单独的包校验值。

## 重建顺序

1. 执行 plugin/build.ps1，参数 ProjectRoot 指向 local、DotnetPath 指向 .NET 10 SDK、DalamudPath 指向 API 15 实际库目录。
2. 通过 plugin/tools/build-dr-dependencies.ps1，从保存的公开源码重建两个依赖至 local/dr-dependencies/rebuilt；审查后更新第三方运行库和 SHA256 清单。
3. 编译审计工具 plugin/tools/DRCompatibilityAudit，传入移植后的源码目录、SDK 目录、宿主与依赖目录、报告目录。环境变量 YANJIU_REGEX_GENERATOR 指向 .NET 10 引用包自带的 System.Text.RegularExpressions.Generator.dll。审计会执行真实正则源码生成、检查有效模块类型并输出逐模块 DLL。
4. 执行 plugin/tools/pack-dr.ps1，ProjectRoot 指向 local、AuditDirectory 指向完整审计目录。
5. 再执行 plugin/build.ps1 复制包和原生 UI 资源。宿主发生变化时，必须重新检查对应模块包。
6. 执行 plugin/tests/run-tests.ps1。YANJIU_OFFLINE_TESTS=1 运行本地回归并明确跳过在线仓库测试；不冒充在线或游戏内验证。

当前仍缺少部分 Expansion 原生接口、原宿主远程提供器、部分特殊传送适配及跨模块调用。没有用空实现或猜测的内存偏移伪装为兼容。

2026-10-16 09:59:37.532，当前更新包在实际游戏中完成宿主热加载；近期对应加载记录未显示 Yanjiu 错误。该记录仅证明宿主启动，不包含上游模块启用或功能验证。

本轮新增配方笔记的 Raphael 结果适配与自动筹备稀有品的独立程序集引用适配；后者仍受 FastGrandCompanyExchange 前置条件限制，不能启用时不得视为功能已可用。详见本地进度。
Raphael 六字段接口依据：https://github.com/AtmoOmen/OmenTools/blob/d985dc6a7a038f1ecb07b3a86e0caad0e5be3db0/Dalamud/IPC/Providers/RaphaelIPC.cs 。对应公开快照保存在本地研究目录；适配器只改用该实际返回契约，不提供模拟求解结果。

当前军票兑换前置模块已完成窗口接口适配，并补齐模块实例 IPC 注册；自动筹备联动的等待时间与兑换上限一致。内置模块IPC使用 Yanjiu.DR.Modules.*，外部脚本调用原版端点时需要使用新端点。窗口数据契约参考：https://github.com/McVaxius/ADS/blob/5123428880faffe27f794f53ba4e43222cd61f22/ADS/Services/ShopPurchaseRuntime.cs ；本地适配器独立实现，只读取公开窗口契约，不引入 ADS 运行库。
## 2026-10-16 内存加载路径修复
游戏内 Dalamud 以字节流加载宿主程序集，Assembly.Location 为空，旧 DRCatalog 因 Path.Combine 的 path1 为 null 而无法展示任何模块。
插件启动时保存 IDalamudPluginInterface.AssemblyLocation；模块目录、宿主 SHA256、语言资源与源编译器统一使用该安装路径。
回归实际用 Assembly.Load(byte[]) 加载新宿主，确认 Location 为空，再读取并校验 DR 303 项、Omni 53 项目录成功。完整离线回归通过。模块行为仍需游戏内逐项验证。
## 2026-10-16 自动加载与分类
启动时直接登记、校验并分帧加载全部 356 个已集成模块，无需逐项导入；每帧最多处理两个构造结果，新模块保持停用。错误按模块隔离。
启用 DR 模块递归启动真实前置依赖，依赖尚在加载时延后重试；检查循环、缺失、同功能和显式冲突。运行中的依赖不能被提前停用。
自动出售幻卡的旧 InstantLeaveDuty 前置名称修正为实际提供 leaveduty 命令的 LeaveDutyCommand。
顶部添加全部、已启用、收藏、系统、系统界面等分类；操作菜单收藏并持久化，收藏靠前。来源标签保留。
FastObjectInteract 依赖已取消的 FastWorldTravel；AutoRetainerWork 依赖已取消的 BetterMarketBoard，两个缺失依赖明确提示，未以空实现代替。
回归包含递归依赖链、循环拒绝、实际冲突、依赖停用保护、分类和收藏保存，以及全部包类型绑定和内存加载场景。
命名占位符支持已回归：实际 LeaveDutyCommandDescription、数字格式及字面大括号均通过，修复批量构造阶段三个模块说明格式异常。
## 2026-10-16 重复模块来源选择
用户选择 12 组重复/重叠功能统一使用 Omni，DR 对应模块不进入自动加载目录：AutoLogin、AutoSortItems、CopyItemNameContextMenu、AutoPreventDuplicateStatus、AutoCastMovementLock、AutoAntiCensorship、AutoCheckFoodUsage、BetterFPSLimitation、OptimizedMacro、UnifiedGlamourManager、AutoDisplayMitigationInfo、InstantLogout。
保留 DR 模块详情显示其真实 ModulesPrerequisite、ModulesConflict、ModulesPair，缺失或未启用的前置仍由启用流程拒绝或自动递归启用；关系来自上游模块元数据，没有补写推测关系。

## 2026-10-16 DR 卡片交互样式
模块标题前添加显式展开/收起箭头。点击箭头或标题可展开设置；停用模块先展示“启用并打开设置”，避免访问 Init 尚未初始化的配置。没有设置的模块显示说明。
说明以 85% 字号和较暗文字显示。前置/冲突/可搭配关系改为标签，仅有关系时显示，标签带实际已启用数/总数；点击弹出带复选框的模块列表，调用实际启停、递归依赖与冲突检查。缺失及待适配模块禁止勾选并提供错误提示。
标签按卡片可用宽度自动换行；标题列弹性伸缩，宽窗口且作者非空时显示较小作者文字。保持当前 FF14 原生配色。
编译、模块类型绑定与完整离线回归通过。尚未进行当前界面的游戏内视觉验收。
