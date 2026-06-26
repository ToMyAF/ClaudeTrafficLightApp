# Claude Code Traffic Light - 安装和使用说明

## 项目结构

```
CCLight/
├── TrafficLightApp/          # WPF桌面红绿灯程序
│   ├── Services/
│   │   ├── WebSocketServer.cs   # WebSocket服务端
│   │   └── SettingsService.cs   # 设置服务
│   ├── App.xaml
│   ├── MainWindow.xaml       # 主窗口（指示灯+气泡消息）
│   └── TrafficLightApp.csproj
└── vscode-extension/         # VSCode配套扩展
    ├── src/
    │   ├── extension.ts      # 扩展入口
    │   ├── HookInjector.ts   # 钩子注入器
    │   └── hook-code.js      # DOM监控钩子代码
    ├── package.json
    └── tsconfig.json
```

## 安装步骤

### 1. 编译WPF桌面程序

```bash
cd TrafficLightApp
dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
```

运行 `publish/ClaudeTrafficLight.exe`

### 2. 安装VSCode扩展

```bash
cd vscode-extension
npm install
npm run compile
npm run copy-hook

# 打包成vsix（需要vsce工具）
npm install -g @vscode/vsce
vsce package

# 安装
code --install-extension claude-traffic-light-connector-0.1.0.vsix
```

### 3. 注入钩子到Claude Code扩展

1. 在VSCode中按 `Ctrl+Shift+P`
2. 运行命令：`Claude Traffic Light: Inject Hook`
3. 重启VSCode

## 使用说明

1. 启动桌面红绿灯程序
2. 打开VSCode和Claude Code
3. 正常使用Claude Code
4. 观察桌面红绿灯：
   - 🟡 黄灯：Claude正在思考/处理中
   - 🟢 绿灯：Claude正在输出结果
   - 🔴 红灯：需要用户确认（闪烁3次后常亮）

## 配置

右键点击红绿灯可以：
- 调整透明度（0.3 - 1.0）
- 退出程序

配置文件保存在：`%APPDATA%\ClaudeTrafficLight\settings.json`

## 故障排除

### 红灯不显示
- 确认钩子已正确注入（重新运行注入命令）
- 确认VSCode已重启
- 查看VSCode扩展输出日志

### 连接失败
- 确认红绿灯程序正在运行
- 检查防火墙设置（端口19876）
- 确认没有其他程序占用19876端口

### Claude Code更新后失效
- 重新运行 `Claude Traffic Light: Inject Hook` 命令
- 重启VSCode

## 卸载

1. 在VSCode中禁用/卸载扩展
2. 删除桌面程序
3. 删除配置文件夹：`%APPDATA%\ClaudeTrafficLight`
