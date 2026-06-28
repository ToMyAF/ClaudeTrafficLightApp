<div align="center">

# 🚦 Claude Traffic Light

Windows 桌面悬浮红绿灯程序，实时监控 VSCode 中 Claude Code 扩展的运行状态

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](#contributing)

[功能特性](#-功能特性) • [快速开始](#-快速开始) • [使用说明](#-使用说明) • [开发指南](#-开发指南)

</div>

---

## ✨ 功能特性

| 灯色 | 状态 | 说明 |
|-----|------|------|
| 🟡 **黄灯** | `thinking` | Claude 正在思考/处理中 |
| 🟢 **绿灯** | `writing` | Claude 正在输出结果/写入文件 |
| 🔴 **红灯** | `needs_confirm` | 需要用户确认/交互 |
| ⚪ **熄灭** | `idle` | 空闲状态 |

- 🎯 **实时监控** - 毫秒级响应 Claude Code 状态变化
- 💬 **气泡消息提示** - 状态变化时显示通知气泡
- 🖼️ **窗口多开支持** - 支持多个 VSCode 窗口状态轮播
- 🎨 **可配置透明度** - 自由调整红绿灯透明度
- 🧲 **边缘自动吸附** - 拖动到屏幕边缘自动对齐
- 🔔 **系统托盘集成** - 最小化到托盘，不占用任务栏
- ⚙️ **高度可配置** - 丰富的自定义选项
- 🚀 **零侵入设计** - 使用 Hook 机制，不修改 Claude Code 内部逻辑

---

## 🚀 快速开始

### 前置要求

- Windows 10 或更高版本
- VSCode
- [Claude Code 扩展](https://marketplace.visualstudio.com/items?itemName=anthropic.claude-code)

### 安装步骤

#### 1. 下载桌面程序

从 [Releases](https://github.com/yourusername/CCLight/releases) 页下载最新版本：

```
ClaudeTrafficLight-v1.0.0.zip
```

解压后直接运行 `ClaudeTrafficLight.exe` 即可（无需安装）。

#### 2. 安装 VSCode 扩展

```bash
cd vscode-extension
code --install-extension claude-traffic-light-connector-0.1.0.vsix
```

#### 3. 开始使用

1. 启动 `ClaudeTrafficLight.exe`
2. 打开 VSCode（扩展会自动激活）
3. 正常使用 Claude Code
4. 信号灯会自动响应状态变化 🎉

---

## 📖 使用说明

### 基本操作

| 操作 | 说明 |
|-----|------|
| **左键拖动** | 移动红绿灯位置 |
| **右键点击** | 打开设置菜单 |
| **双击托盘图标** | 显示/隐藏红绿灯 |
| **关闭窗口** | 最小化到系统托盘 |

### 快捷键

在 VSCode 中按 `Ctrl+Shift+P` 打开命令面板，搜索 "Claude Traffic Light"：

| 命令 | 说明 |
|-----|------|
| `Show Status` | 显示当前连接状态 |
| `Reconnect WebSocket` | 重新连接 WebSocket |
| `Set Thinking` | 🟡 设置为思考状态 |
| `Set Writing` | 🟢 设置为输出状态 |
| `Set Confirm` | 🔴 设置为需要确认状态 |
| `Set Idle` | ⚪ 设置为空闲状态 |
| `Quick Switch` | 快速切换面板 |

### 配置选项

在 VSCode 设置中搜索 "Claude Traffic Light"：

| 配置项 | 默认值 | 说明 |
|-------|-------|------|
| `websocket.host` | `localhost` | WebSocket 服务器地址 |
| `websocket.port` | `19876` | WebSocket 服务器端口 |
| `websocket.maxReconnectAttempts` | `10` | 最大重连尝试次数（0=无限） |
| `showStateChanges` | `false` | 在控制台显示所有状态变化 |

---

## 🏗️ 架构设计

采用 **Claude Code Hooks + 文件轮询 + WebSocket** 方案，简单可靠：

```
┌─────────────────┐    写文件        ┌─────────────────────┐
│  VSCode         │ ───────────────> │  状态文件           │
│  Claude Code    │   (3个Hooks)     │  ~/.claude/         │
│  Extension      │                  │  cc_traffic_light_state
└─────────────────┘                  └──────────┬──────────┘
                                                │ 轮询读取
                                                ▼
┌─────────────────┐    WebSocket     ┌─────────────────────┐
│  桌面红绿灯     │ <────────────── │  VSCode扩展         │
│  (WPF程序)      │                  │  (localhost:19876)  │
└─────────────────┘                  └─────────────────────┘
```

### 为什么这个方案最稳定？

- ✅ **无侵入性** - 不修改 Claude Code 进程内部逻辑
- ✅ **简单可靠** - 只有 3 个核心 Hook，不会影响性能
- ✅ **容错性好** - Hook 失败不影响主程序运行
- ✅ **经验证** - uni-pet 等工具采用的成熟方案

---

## 🔧 开发指南

### 环境要求

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 或 VSCode
- Node.js 16+（用于 VSCode 扩展开发）

### 本地编译

```bash
# 克隆仓库
git clone https://github.com/yourusername/CCLight.git
cd CCLight

# 编译桌面程序
cd TrafficLightApp
dotnet build --configuration Release

# 运行程序
bin\Release\net8.0-windows\ClaudeTrafficLight.exe
```

### 开发 VSCode 扩展

```bash
cd vscode-extension
npm install
npm run compile

# 按 F5 启动扩展开发主机
```

### 发布构建

```bash
cd TrafficLightApp

# 使用发布脚本（推荐）
publish.bat

# 或手动发布
dotnet publish -c Release -r win-x64 --self-contained true
```

---

## 🤝 贡献指南 <a name="contributing"></a>

欢迎贡献！请查看 [CONTRIBUTING.md](CONTRIBUTING.md) 了解详细流程。

### 快速开始贡献

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启 Pull Request

### 可以贡献的方向

- 🐛 报告 Bug 或提交修复
- ✨ 新增功能或改进现有功能
- 📚 改进文档或翻译
- 🎨 UI/UX 优化
- 🔧 开发工具或流程改进

---

## ❓ 故障排查

### 问题1：信号灯不亮

**解决方案：**
1. 确认桌面程序已启动
2. 右键红绿灯 → 查看端口是否为 `19876`
3. 手动测试状态文件：
   ```cmd
   echo yellow > %USERPROFILE%\.claude\cc_traffic_light_state
   ```
   如果黄灯亮起，说明 Hook 和文件系统正常工作。

### 问题2：状态不同步

**解决方案：**
1. 重启 Claude Code（VSCode 窗口）使 Hooks 生效
2. 检查 `C:\Users\lixf\.claude\settings.json` 中 hooks 配置
3. 查看 VSCode 扩展输出日志（Ctrl+Shift+U）

### 问题3：Hook 导致 Claude Code 异常

**解决方案：**
- 我们只保留了 3 个最核心的 Hook 事件，已最小化影响
- 如果仍有问题，可以通过 VSCode 命令临时手动控制

---

## 📝 变更日志

详细的版本变更记录请查看 [CHANGELOG.md](CHANGELOG.md)。

---

## 📄 许可证

本项目采用 [MIT 许可证](LICENSE) 开源。

---

## ❤️ 致谢

- 感谢 [Anthropic](https://www.anthropic.com/) 开发的 Claude Code
- 灵感来自 [uni-pet](https://github.com/timqian/uni-pet) 项目
- 感谢所有贡献者的努力！

---

<div align="center">

**如果这个项目对你有帮助，请给个 ⭐ Star 支持一下！**

Made with ❤️ for the Claude community

</div>
