# Yanjiu · 小烟酒

FF14 游戏聊天与手机交互项目，统一管理 Dalamud 插件和 Android App。

## 项目结构

| 目录 | 内容 |
| --- | --- |
| `plugin/` | 独立卫月插件、DR/Omni 源码兼容层、六套主题和测试 |
| `android/` | 小烟酒 Android App 完整源码、界面和构建脚本 |
| `legacy/` | 当前可用的 Omni `PhoneChat.cs` 模块，作为迁移基线 |
| `third_party/` | 被嵌入库的对应源码、版权、许可证和重建脚本 |
| `docs/` | 开发约定和发布说明 |

Android / 旧模块导入基线为 0.3.34。独立插件 0.1.0 已完成本地 SDK 构建及模块编译测试，属于开发测试版，尚未通过真实游戏与手机联调；`legacy/PhoneChat.cs` 保留为 Omni 迁移基线。

Android App 已具备设备验证码授权、聊天窗口同步、分页缓存、自定义频道、外观设置及通知功能。模块支持个人推送和内嵌 QQ 接入；真实 QQ 登录仍需可用的 V1 签名服务和匹配协议版本。

## 构建与发布

Android 构建见 [构建说明](android/README.md)。独立插件迁移计划见 [插件说明](plugin/README.md)。
测试安装包、工具、签名密钥和私人配置放在本地，不提交到 Git。
插件测试安装包通过本仓库 Releases 管理。第三方插件库地址为：

```text
https://raw.githubusercontent.com/Xiao-Yanjiu/Yanjiu/main/Pluginmaster.json
```

当前只发布测试版本，需要在卫月中允许测试版插件。不内置手机支持，相关功能通过外部模块按需添加。

## 许可证

当前集成代码按 GPL-3.0-or-later 提供，保留各第三方组件原许可证。
QQ 所用 Lagrange V1 的对应源码和构建脚本随仓库提供；不使用授权未明确的 V2。
发布时必须保留完整对应源码和许可材料。游戏名称、角色图像及其他第三方素材的权利属于各自权利人，代码许可证不替代素材授权。
