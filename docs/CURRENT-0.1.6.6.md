# 0.1.6.6 当前开发快照（2026-11-16）

本次归档包含 KamiToolKit 主面板、模块设置页面、五个内置模块，以及多功能工具栏本体和运行时菜单的当前实现。模块 DLL 名称保持不变。

- 五个内置模块对应源码：`plugin/BundledModules/`。保留各文件上游作者和许可证声明，按仓库 GPL 许可证发布。
- 主插件构建：`plugin/Yanjiu/Yanjiu.csproj`；使用 Dalamud API 15 / .NET 10。
- 先构建主插件，再构建各 `plugin/BundledModules/*/*.csproj`，传入 `SdkPath`（Dalamud SDK）与 `ProviderPath`（API1 依赖目录）。模块默认引用 `third_party/api1-runtime`。
- 打包五个模块：`plugin/tools/PackageYanjiuModules.ps1`，传入插件输出目录和包含五个 DLL 的模块输出目录。
- KamiToolKit 对原生回调委托增加生命周期保护，释放组件前还原虚表，避免窗口尺寸变动后调用已回收委托。
- 当前安装包为开发测试快照，保留游戏内验证限制：快捷指令菜单与原生菜单的视觉一致性仍待完善；未把聊天、物品模块源码调查视作已实现功能。

本地旧安装包、私人配置、构建工具及桌面备份不提交到公共仓库。
