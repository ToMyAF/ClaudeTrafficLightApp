<div align="center">

# 📦 安装指南

Claude Traffic Light - 详细安装指南

</div>

---

## 📋 目录

- [系统要求](#系统要求)
- [快速安装](#快速安装)
- [详细安装步骤](#详细安装步骤)
- [验证安装](#验证安装)
- [卸载方法](#卸载方法)
- [常见问题](#常见问题)

---

## 💻 系统要求

### 最低配置

| 组件 | 最低要求 |
|-----|---------|
| **操作系统** | Windows 10 版本 1607 或更高版本 |
| **架构** | x64 (64位) |
| **内存** | 100MB 可用 RAM |
| **磁盘空间** | 200MB 可用空间 |
| **.NET** | 无需预装（自包含发布） |

### 推荐配置

- Windows 11
- 4GB+ RAM
- SSD 固态硬盘

---

## ⚡ 快速安装

### 三步快速开始

```
1️⃣ 下载 ClaudeTrafficLight-v1.0.0.zip
2️⃣ 解压到任意文件夹
3️⃣ 双击 ClaudeTrafficLight.exe 运行
```

---

## 📝 详细安装步骤

### 第一步：下载程序

#### 方式一：从 GitHub Releases 下载（推荐）

1. 访问 [Releases 页面](https://github.com/ToMyAF/ClaudeTrafficLightApp/releases)
2. 找到最新版本
3. 下载 `ClaudeTrafficLight-vX.X.X.zip` 文件

#### 方式二：本地编译（适合开发者）

```bash
# 克隆仓库
git clone https://github.com/ToMyAF/ClaudeTrafficLightApp.git
cd CCLight/TrafficLightApp

# 编译发布
publish.bat

# 输出位置: publish\ClaudeTrafficLight-vX.X.X\
```

### 第二步：解压文件

1. 右键点击下载的 ZIP 文件
2. 选择 **"全部提取..."**
3. 选择一个目标文件夹，例如：
   ```
   C:\Program Files\Claude Traffic Light\
   D:\Tools\ClaudeTrafficLight\
   ```

> 💡 **提示：建议解压到非系统保护的文件夹，避免权限问题

### 第三步：运行程序

1. 进入解压后的文件夹
2. 双击 `ClaudeTrafficLight.exe`
3. 程序启动后，你会看到：
   - ✅ 屏幕上出现红绿灯悬浮窗
   - ✅ 系统托盘出现红绿灯图标
   - ✅ 右下角弹出启动通知

### 第四步：创建快捷方式（可选）

1. 右键点击 `ClaudeTrafficLight.exe`
2. 选择 **"发送到"** → **"桌面快捷方式"**
3. （可选）右键快捷方式 → **"属性"**，设置：
   - 快捷键（如 `Ctrl+Alt+T`）
   - 运行方式（最小化等）

---

## 🔌 VSCode 扩展安装

### 安装扩展

```bash
# 进入扩展目录
cd vscode-extension

# 安装 VSIX 包
code --install-extension claude-traffic-light-connector-0.1.0.vsix
```

### 验证扩展安装

1. 打开 VSCode
2. 按 `Ctrl+Shift+X` 打开扩展面板
3. 搜索 "Claude Traffic Light Connector"
4. 确认扩展已启用

---

## ✅ 验证安装

### 检查清单

- [ ] 桌面程序能正常启动
- [ ] 系统托盘显示红绿灯图标
- [ ] VSCode 扩展已启用
- [ ] 状态变化能正确显示

### 功能测试

#### 测试 1：手动控制状态

1. 在 VSCode 中按 `Ctrl+Shift+P`
2. 输入 `Claude Traffic Light: Set Thinking`
3. 确认红绿灯变为黄色 🟡

#### 测试 2：测试文件轮询

1. 打开命令提示符
2. 输入：
   ```cmd
   echo red > %USERPROFILE%\.claude\cc_traffic_light_state
   ```
3. 确认红绿灯变为红色 🔴

#### 测试 3：WebSocket 连接

1. 右键托盘图标 → 查看状态
2. 确认显示 "已连接" 或查看日志

---

## 🚪 卸载方法

### 完全卸载步骤

1. **退出程序**
   - 右键系统托盘图标
   - 选择 "退出"

2. **删除程序文件**
   - 删除解压的文件夹
   - 删除桌面快捷方式

3. **删除配置文件**（可选）
   ```cmd
   # 删除状态文件
   del %USERPROFILE%\.claude\cc_traffic_light_state

   # 删除配置（如果有的话）
   del %APPDATA%\ClaudeTrafficLight\config.json
   ```

4. **卸载 VSCode 扩展**
   - 在 VSCode 扩展面板中找到扩展
   - 点击 "卸载"

---

## ❓ 常见问题

### Q: 程序无法启动？

**A:** 请尝试以下解决方案：
1. 确认使用的是 64 位 Windows
2. 右键 → "以管理员身份运行"
3. 检查 Windows Defender 是否阻止了程序
4. 确认下载的文件完整（文件大小约 170MB）

### Q: 托盘图标不显示？

**A:**
1. 点击系统托盘的向上箭头（^）
2. 找到 Claude Traffic Light 图标
3. 右键 → 拖到任务栏固定显示

### Q: VSCode 连接失败？

**A:**
1. 确认桌面程序已启动
2. 检查端口 19876 是否被占用
3. 重启 VSCode
4. 检查防火墙设置

### Q: 如何设置开机启动？

**A:**
1. `Win+R` 输入 `shell:startup`
2. 将程序快捷方式复制到打开的文件夹
3. 下次开机时会自动启动

### Q: 在哪里可以找到旧版本？

**A:** 所有历史版本都可以在 [GitHub Releases](https://github.com/ToMyAF/ClaudeTrafficLightApp/releases) 页面找到。

---

## 📞 需要帮助？

如果本指南没有解决你的问题：

1. 查看 [README.md](README.md) 中的故障排查部分
2. 搜索 [GitHub Issues](https://github.com/ToMyAF/ClaudeTrafficLightApp/issues)
3. 如未找到解决方案，创建新的 Issue

---

## 🔄 更新程序

### 自动更新（计划中）

未来版本将支持自动更新。

### 手动更新

1. 退出当前运行的程序
2. 下载新版本的 ZIP 文件
3. 覆盖旧版本的文件
4. 重新启动程序

> 💡 **提示：配置和设置会自动保留

---

<div align="center">

**安装成功！享受你的 Claude Traffic Light！ 🎉

如有问题，请查看 [README.md](README.md) 或提交 Issue。

</div>
