<div align="center">

# 🤝 贡献指南

感谢你对 Claude Traffic Light 项目感兴趣！

我们欢迎所有形式的贡献，无论是报告 Bug、提出功能建议、改进文档，还是直接提交代码。

</div>

---

## 📋 目录

- [行为准则](#行为准则)
- [如何贡献](#如何贡献)
  - [报告 Bug](#报告-bug)
  - [提出功能建议](#提出功能建议)
  - [提交代码](#提交代码)
- [开发环境搭建](#开发环境搭建)
- [代码规范](#代码规范)
- [Pull Request 流程](#pull-request-流程)
- [社区交流](#社区交流)

---

## 📜 行为准则

### 我们的承诺

为了营造开放友好的社区环境，我们承诺：
- 欢迎所有背景和经验水平的贡献者
- 保持尊重、专业的交流态度
- 专注于对社区最有利的事情
- 接受建设性的批评

### 我们的标准

积极的行为包括：
- 使用友好和包容的语言
- 尊重不同的观点和经验
- 优雅地接受建设性批评
- 关注社区的整体利益

---

## 🚀 如何贡献

### 🐛 报告 Bug

如果你发现了 Bug，请通过 [GitHub Issues](https://github.com/ToMyAF/ClaudeTrafficLightApp/issues) 报告。

**报告 Bug 时请包含以下信息：**

1. **Bug 描述** - 清晰简洁地描述问题
2. **复现步骤** - 如何复现这个 Bug
   ```
   1. 打开 '...'
   2. 点击 '....'
   3. 滚动到 '....'
   4. 看到错误
   ```
3. **预期行为** - 你期望发生什么
4. **实际行为** - 实际发生了什么
5. **截图** - 如果适用，添加截图帮助说明问题
6. **环境信息**：
   - Windows 版本
   - .NET 版本
   - VSCode 版本
   - Claude Code 扩展版本
7. **其他上下文** - 任何其他相关的信息

---

### 💡 提出功能建议

我们欢迎各种功能建议！请通过 [GitHub Issues](https://github.com/ToMyAF/ClaudeTrafficLightApp/issues) 提交。

**功能建议应包含：**

1. **功能描述** - 清晰简洁地描述你想要的功能
2. **使用场景** - 这个功能解决了什么问题
3. **解决方案** - 你认为应该如何实现
4. **替代方案** - 你考虑过的其他方案
5. **附加信息** - 截图、参考链接等

---

### 💻 提交代码

**准备工作：**

1. 确保你已经安装了必要的开发工具
2. 阅读本文档的开发环境搭建部分
3. 搜索现有的 Issues 和 PRs，避免重复工作

**代码贡献流程：**

1. Fork 本仓库
2. 创建你的特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交你的更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 开启一个 Pull Request

---

## 🔧 开发环境搭建

### 前置要求

| 工具 | 版本要求 | 说明 |
|-----|---------|------|
| .NET SDK | 8.0+ | 桌面程序开发 |
| Node.js | 16.0+ | VSCode 扩展开发 |
| VSCode | 最新版 | 推荐的 IDE |
| Git | 任意版本 | 版本控制 |

### 克隆仓库

```bash
git clone https://github.com/yourusername/CCLight.git
cd CCLight
```

### 桌面程序开发

```bash
cd TrafficLightApp

# 还原依赖
dotnet restore

# 构建
dotnet build

# 运行
dotnet run

# 发布
publish.bat
```

### VSCode 扩展开发

```bash
cd vscode-extension

# 安装依赖
npm install

# 编译
npm run compile

# 按 F5 启动扩展开发主机
```

---

## 📝 代码规范

### C# 代码规范

我们遵循 [Microsoft C# 编码规范：

- 使用 PascalCase 命名类、方法、属性
- 使用 camelCase 命名局部变量和参数
- 使用 PascalCase 命名常量
- 接口以 `I` 开头（如 `IWebSocketServer`）
- 私有字段以 `_` 开头（如 `_notifyIcon`）
- 使用 4 空格缩进（不要使用 Tab）
- 每行不超过 120 字符
- 使用有意义的命名，避免缩写
- 为公共 API 添加 XML 文档注释

**示例：**

```csharp
/// <summary>
/// WebSocket 服务器接口
/// </summary>
public interface IWebSocketServer
{
    /// <summary>
    /// 启动服务器
    /// </summary>
    /// <param name="port">监听端口</param>
    Task StartAsync(int port);
}
```

### TypeScript 代码规范

- 遵循 [TypeScript 官方风格指南](https://github.com/Microsoft/TypeScript/wiki/Coding-guidelines)
- 使用 ESLint 检查代码
- 运行 `npm run lint` 进行代码检查

### Git 提交规范

我们使用 [Conventional Commits](https://www.conventionalcommits.org/) 规范：

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Type 类型：**

| 类型 | 说明 |
|-----|------|
| `feat` | 新功能 |
| `fix` | Bug 修复 |
| `docs` | 文档更新 |
| `style` | 代码格式调整（不影响代码运行） |
| `refactor` | 重构（既不是新增功能，也不是修改 bug） |
| `perf` | 性能优化 |
| `test` | 测试相关 |
| `chore` | 构建过程或辅助工具的变动 |

**示例：**

```
feat(tray): add dynamic icon generation

- 使用 GDI+ 动态生成红绿灯图标
- 移除对外部 icon.ico 文件的依赖
- 添加状态感知的图标更新

Closes #123
```

---

## ✅ Pull Request 流程

### PR 准备清单

提交 Pull Request 前，请确保：

- [ ] 代码已通过本地编译测试
- [ ] 遵循了项目的代码规范
- [ ] 更新了相关文档（README、CHANGELOG 等）
- [ ] 添加了必要的注释
- [ ] 提交信息符合规范
- [ ] PR 标题清晰描述了更改内容
- [ ] PR 描述详细说明了更改的原因和内容

### PR 模板

```markdown
## 📝 变更描述

清晰简洁地描述这个 PR 做了什么。

## 🔗 关联 Issue

Fixes #123

## 📷 截图（如适用）

## ✅ 检查清单

- [ ] 代码已通过本地编译
- [ ] 遵循了代码规范
- [ ] 更新了相关文档
- [ ] 添加了必要的注释
- [ ] 更新了 CHANGELOG.md
```

### 代码审查

- 所有 PR 都需要至少一位维护者审查
- 请耐心等待审查，我们会尽快处理
- 审查意见请保持尊重和专业
- 根据审查意见进行修改和补充

---

## 💬 社区交流

- **GitHub Issues** - Bug 报告和功能建议
- **GitHub Discussions** - 一般讨论和问答
- **Pull Requests** - 代码审查和讨论

### 寻求帮助

如果你有问题：

1. 先搜索现有的 Issues 和 Discussions
2. 查看 README 和文档
3. 如果还是找不到答案，创建新的 Discussion

---

## 🎉 感谢贡献

每一份贡献都很重要！

你的名字将会出现在 [贡献者列表](#) 中。

---

## 📄 许可证

通过贡献，你同意你的贡献将根据项目的 [MIT 许可证](LICENSE) 授权。

---

<div align="center">

**再次感谢你的贡献！ ❤️

</div>
