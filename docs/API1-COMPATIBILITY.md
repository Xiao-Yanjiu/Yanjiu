# Yanjiu 0.1.6：七模块 API 1 兼容候选版

接口依据：https://github.com/YouShux/OmniToolbox.TreeHouseOnline ，2026-10-29 核对；API 1，最低 Omni 规范 1.1.8.2。本候选版固定使用本机 1.1.8.7 所附公开 Common、TreeTools、TreeToolKit 及运行依赖，摘要在 third_party/api1-runtime/SHA256.json。没有引入 Omni 私有 Tree、TreePublic 或主插件 DLL。

## 实现

- API 1 模块使用真实公共程序集，在独立 AssemblyLoadContext 中运行；旧 DR 使用原有运行库。编译 API 1 源码时不混入旧宿主兼容类型，浏览器 DLL 可保持原始字节。
- 检查基类、具体类 ModuleApiVersion、单文件单模块、公共无参构造，以及清单版本、来源与 Localization.Source。API 失败时清单回退到 raw；安装或恢复前匿名确认仓库公开。
- 通过实际 OmniModuleBase 异步生命周期启动、停止、等待后台工作和最终释放。宿主使用 IAsyncDalamudPlugin，异步退出结束前不释放依赖。启停期间关闭宏入口。
- 绑定模块内名为 config 的 Settings；每次保存读取当前字段，支持 ResetSettings 替换对象。JSON 配置保持作者键名，在 Windows 当前用户 DPAPI 信封内保存；不把音乐登录 Cookie 写入明文宿主配置。
- 独立词库登记、中文标题与说明、图标浏览器、宿主宏路由、未启用时的配置入口。API 1 服务配置独立于旧 DR 服务配置目录。
- 在线模块菜单提供更新到清单当前版本的入口。旧接口缓存保留文件、配置和启用意愿，但不自动运行；更新到 API 1 后再加载。

## 七个实际发布文件

来源：https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu 。本次检查的是清单版本 2.0.0 的 MusicPlayer、GameWebBrowser、AutoDiscardJunk、ToolbarIconPlus、IChingModule、CommandCatalog、PhoneChat；文件 SHA256 与公开清单一致。模块文件未修改。

全部通过编译或 DLL 加载、API 契约、中文词库及配置结构检查；六个具有 Settings 字段的模块通过加密序列化/类型往返。CommandCatalog 无持久配置字段。

额外合成测试覆盖启动等待、停止时关闭命令、取消与等待后台任务、停用/启用复用、最终释放、配置对象替换、加密读取、保存失败回滚、API 声明拒绝、API→raw 回退、公开仓库检查与仓库替换拒绝。

原有 DR 311 项目录、303 项编译/绑定及旧宿主离线回归通过；另外 8 项仍是既有待适配项。

未执行七模块的 OnStart 游戏操作，没有验证真实游戏内的原生 Hook、浏览器内核、音乐账号、QQ/手机服务或角色操作。离线通过不等于上述功能均已完成游戏内验证。本版作为 0.1.6 测试版发布；游戏内验证范围仍以上述说明为准。

## 使用候选版

1. 停用旧版 Yanjiu，避免同时运行两个宿主实例。
2. 解压候选包，在卫月开发插件设置中添加解压目录的 Yanjiu.dll，保留 Api1、DR、Omni、Assets 等目录。
3. 加载后刷新原有在线仓库，从各模块操作菜单“更新到 2.0.0”；不需要重复添加备用链接。随后按需启用模块。
4. 先检查启用、关闭窗口、停用与重新启用是否正常，再验证具体游戏功能；发生初始化错误时查看宿主诊断与 xllog。

候选版最低卫月版本为 15.0.3.9（API 15），本地构建使用 26-10-21-01 SDK / .NET 10。

## 重建和测试

```powershell
./plugin/tools/rebuild-release.ps1 -DalamudPath '<SDK>' -DotnetPath '<dotnet.exe>'
dotnet run --project ./plugin/tools/Api1CompatibilityCheck/Api1CompatibilityCheck.csproj -c Release -p:YanjiuArtifactPath='<候选插件目录>' -- '<候选插件目录>' '<SDK>' '<你的模块仓库本地目录>'
```

检测工具仅在独立进程中创建真实模块元数据并测试 Settings，不启用实际模块。生命周期用独立合成模块测试，不触发真实游戏操作。
