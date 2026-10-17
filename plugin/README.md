# 小烟酒 · Yanjiu 0.1.5 开发测试版

国服卫月 API 15 / .NET 10。入口 `/yanjiu`、`/xyj`；音乐播放器入口 `/ymusic`（模块须已安装并启用）；图标浏览器入口 `/yanjiu icons`。

## 安装

卫月第三方仓库地址：`https://raw.githubusercontent.com/Xiao-Yanjiu/Yanjiu/main/Pluginmaster.json`。本版以测试版本发布，需允许测试版插件。安装器从本仓库 Releases 下载完整 ZIP；开发插件可导入解压后的 Yanjiu.dll，保留同目录依赖、DR、Omni、Assets 和许可证。

## 功能与边界

- 在线、本地模块管理，全部/已启用/收藏/在线/本地筛选；已下载在线模块有直接删除按钮。
- FF14 原生配色、完整图标浏览器、模块图标选择、独立字体与布局缩放。
- 宿主按模块名转发宏命令，提供参数保留和禁用模块检查。`/omni` 未被占用时可作兼容别名；同时使用 Omni 时使用 `/yanjiu MusicPlayer`，不会接管 Omni 命令。
- DR 311 个目录项，303 个编译及类型绑定通过，8 个因原宿主接口缺失不可启用。模块间前置、冲突和可搭配关系会显示。
- 不内置 Omni 功能模块，不内置手机支持。外部模块按需下载，首次默认关闭；原 Omni 宿主 DLL 不能直接当作 Yanjiu 模块 DLL。
- 启动校验宿主及模块哈希，不匹配时隔离错误。导入代码具有插件进程权限，不是沙箱。

编译、内存加载、生命周期、依赖与冲突、配置、命令转发、删除路径边界和包哈希已通过离线回归。并未逐项执行所有上游模块的游戏操作，也未完成所有外部服务联调；因此保持开发测试标记。

## 从对应源码重建

安装 .NET SDK 10.0.401、PowerShell 7，准备 API 15 的卫月库目录。在仓库根目录运行：

```powershell
./plugin/tools/rebuild-release.ps1 -DalamudPath '<卫月 API 15 目录>' -DotnetPath '<dotnet.exe>'
```

该脚本从 third_party/sources 初始化本地工作目录，构建宿主，编译与绑定审计公开 DR 模块，生成模块目录及哈希，重新构建完整包并运行离线测试。输出位于 local/release-work/staging/plugin；local 不上传。

运行库使用 third_party/plugin-runtime/SHA256.json 固定校验。修改版 OmenTools 及两个原生 UI 依赖的完整源码在 third_party/sources；可使用 plugin/tools/rebuild-omen.ps1 和 build-dr-dependencies.ps1 重建。重建依赖后应更新校验清单并重新运行完整构建测试。

打包前确认主 DLL 与 DR/Omni/Modules/host.sha256 相同；部署可使用 plugin/tools/deploy-plugin.ps1，先校验与备份，最后复制主 DLL。

源码、各组件许可证、构建脚本与插件在同一 GitHub 仓库及发行标签保存。既有 Android App 和 legacy/PhoneChat.cs 保留，不作为本版新增功能。
