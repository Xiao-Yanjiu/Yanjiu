# Yanjiu 原生 UI 公共接口 v1

最低宿主为 Yanjiu 0.1.6.5。本程序集不引用 ImGui、KamiToolKit 或 OmenTools，模块只需要引用 Yanjiu.NativeUI.Contracts.dll。渲染由宿主的 KamiToolKit 完成，避免模块混用两套原生运行库。

## 模块设置入口

实现 INativeModuleUI.CreateNativePanel()，返回 NativePanel。宿主点击设置时优先使用它，没有实现的 DR/API1 模块继续通过配置模型生成原生字段。
支持 NativeLabel、NativeButton、NativeToggle、NativeText、NativeNumber。控件回调在游戏框架线程执行，Enabled 可用于判断按钮是否可用。数值输入使用点号作为小数点，宿主检查范围与有限值。
写入或操作完成后，宿主调用模块已有的配置保存接口。操作回调应当只入队耗时任务，并自行检查游戏状态。

## 模块专用窗口

启用完成后调用 await NativeUi.OpenAsync(this, panel)。同一模块使用相同 panel.Id 会替换旧窗口，不同 Id 可同时存在。关闭使用 await NativeUi.CloseAsync(this, panel.Id)。宿主负责框架线程调度、窗口编号、停用/卸载后的清理和宿主关闭。
不要在 OnStart 中等待打开窗口，该阶段启用尚未完成。可在宏命令或启用完成后的事件中调用。模块内部直接创建的 KamiToolKit 窗口仍由模块自行释放。

API1 的隔离加载上下文固定共享宿主的契约程序集。不要把另一份契约复制到 Api1 目录，也不要把主插件 Yanjiu.dll 作为模块引用，否则会与原宿主兼容类型冲突。

可导入的单文件示例在 ../NativeUI.Examples/NativeExample.cs。开启模块后使用 /yanjiu NativeExample open，或者点击模块设置。示例不包含 ImGui 绘制代码。

当前 v1 覆盖基础表单与操作面板。复杂表格、图标网格、拖拽、游戏专用预览等仍需扩展公共控件，旧模块的 DrawSettings 和独立窗口不会自动转换。
编译与加载测试不执行游戏原生窗口，请在游戏内检查输入、焦点、关闭和重新启用。
