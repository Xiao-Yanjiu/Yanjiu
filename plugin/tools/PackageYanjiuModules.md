# Yanjiu 随包模块

插件启动会读取 `Yanjiu/Modules/catalog.json`，校验宿主与模块 SHA256，再通过现有 API 1 运行时加载这五个模块。它们不再需要手工导入本地路径。

在主插件及五个模块编译、兼容性检查通过后执行：

```powershell
./PackageYanjiuModules.ps1 -PluginDirectory <插件包目录> -ModuleDirectory <五个模块DLL目录>
```

五个 DLL 保留 `Yanjiu.Modules.<模块名>.dll` 的名称。模块对应源码当前位于仓库 `plugin/BundledModules`；发布时仍需携带这些对应源码和许可证。本脚本只打包，不执行模块或重新审计 DR/Omni 包。

首次加载按 DLL 文件名将原本地记录迁移为随包记录，保留收藏和启用意图，删除重复旧身份。模块配置继续使用原有 `modules/api1/<模块类型名>.json`，不因来源变化而另建配置。原来的本地 DLL 文件保留。

