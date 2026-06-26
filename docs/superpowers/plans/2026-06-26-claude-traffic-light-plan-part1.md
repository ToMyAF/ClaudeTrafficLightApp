# Claude Code 红绿灯 - 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个Windows桌面悬浮红绿灯程序，实时监控VSCode中Claude Code扩展的运行状态，并通过颜色（黄/绿/红）和气泡提示用户。

**Architecture:** 客户端-服务器架构。WPF桌面程序作为WebSocket服务端监听本地端口，VSCode配套扩展注入钩子到Claude Code官方扩展中，通过WebSocket实时推送状态变化。

**Tech Stack:** .NET 8 WPF, TypeScript (VSCode Extension), WebSocket, VSCode Extension API

## Global Constraints

- 仅支持Windows平台
- .NET 8 或更高版本
- VSCode 1.80+
- TypeScript 5.0+
- 依赖最小化原则，尽量使用系统内置库

---

## 第一部分：项目初始化与基础结构

### Task 1: Git仓库初始化

**Files:**
- Create: `.gitignore`
- Create: `README.md`

**Interfaces:**
- Produces: 基础git仓库结构

- [ ] **Step 1: 初始化git仓库**

Run: `git init`

- [ ] **Step 2: 创建.gitignore文件**

```gitignore
# .NET
bin/
obj/
.vs/
*.user
*.userosscache
*.suo
*.cache
*.log
.idea/
*.swp

# Node.js
node_modules/
npm-debug.log
yarn-error.log
.vscode-test/
*.vsix

# Build
dist/
out/
publish/

# Config
appsettings.local.json
```

- [ ] **Step 3: 创建README.md**

```markdown
# Claude Code Traffic Light

Windows桌面悬浮红绿灯程序，实时监控VSCode中Claude Code扩展的运行状态。

## 功能特性

- 🟡 黄灯：Claude正在思考/处理中
- 🟢 绿灯：Claude正在输出结果/写入文件
- 🔴 红灯：需要用户确认/交互（闪烁3次后常亮）
- 气泡消息提示
- 支持多VSCode窗口轮播
- 可配置透明度

## 项目结构

- `/TrafficLightApp` - WPF桌面红绿灯程序
- `/vscode-extension` - VSCode配套扩展
```

- [ ] **Step 4: 首次提交**

Run:
```
git add .gitignore README.md
git commit -m "feat: init project structure"
```

---

### Task 2: WPF项目初始化

**Files:**
- Create: `TrafficLightApp/TrafficLightApp.csproj`
- Create: `TrafficLightApp/App.xaml`
- Create: `TrafficLightApp/App.xaml.cs`
- Create: `TrafficLightApp/MainWindow.xaml`
- Create: `TrafficLightApp/MainWindow.xaml.cs`

**Interfaces:**
- Produces: 可运行的WPF应用基础框架

- [ ] **Step 1: 创建WPF项目文件**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <AssemblyName>ClaudeTrafficLight</AssemblyName>
    <RootNamespace>ClaudeTrafficLight</RootNamespace>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: 创建App.xaml**

```xml
<Application x:Class="ClaudeTrafficLight.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: 创建App.xaml.cs**

```csharp
using System.Windows;
namespace ClaudeTrafficLight;
public partial class App : Application
{
}
```

- [ ] **Step 4: 创建MainWindow.xaml（基础无边框窗口）**

```xml
<Window x:Class="ClaudeTrafficLight.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Claude Traffic Light"
        Width="120"
        Height="60"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False">
    <Grid>
        <Border Background="#202020"
                CornerRadius="8"
                Opacity="0.8">
            <StackPanel Orientation="Horizontal"
                        HorizontalAlignment="Center"
                        VerticalAlignment="Center">
                <Ellipse x:Name="StatusLight"
                         Width="24"
                         Height="24"
                         Fill="#888888"/>
            </StackPanel>
        </Border>
    </Grid>
</Window>
```

- [ ] **Step 5: 创建MainWindow.xaml.cs**

```csharp
using System.Windows;
using System.Windows.Input;
namespace ClaudeTrafficLight;
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => DragMove();
    }
}
```

- [ ] **Step 6: 验证项目可以编译运行**

Run: `cd TrafficLightApp; dotnet run`
Expected: 窗口显示，可拖动，显示灰色圆点

- [ ] **Step 7: 提交**

Run:
```
git add TrafficLightApp/
git commit -m "feat: init WPF project with basic draggable window"
```

---

## 第二部分：WebSocket通信层

### Task 3: WebSocket服务端实现

**Files:**
- Create: `TrafficLightApp/Services/WebSocketServer.cs`
- Modify: `TrafficLightApp/TrafficLightApp.csproj`
- Modify: `TrafficLightApp/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: .NET System.Net.WebSockets
- Produces: `WebSocketServer.StartAsync(int port)`, `StateChanged` event

- [ ] **Step 1: 创建WebSocket服务端类**

```csharp
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
namespace ClaudeTrafficLight.Services;
public class WebSocketMessage
{
    public string type { get; set; } = string.Empty;
    public JsonElement payload { get; set; }
}
public class StateChangePayload
{
    public string state { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
    public string vscodeWindowId { get; set; } = string.Empty;
    public long timestamp { get; set; }
}
public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<WebSocket> _clients = new();
    public event EventHandler<StateChangePayload>? StateChanged;
    public int Port { get; }
    public WebSocketServer(int port = 19876)
    {
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }
    public async Task StartAsync()
    {
        _listener.Start();
        _ = Task.Run(AcceptConnectionsLoop);
    }
    private async Task AcceptConnectionsLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync();
            if (context.Request.IsWebSocketRequest)
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                _clients.Add(wsContext.WebSocket);
                _ = Task.Run(() => HandleClient(wsContext.WebSocket));
            }
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }
    private async Task HandleClient(WebSocket ws)
    {
        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                await ProcessMessage(message);
            }
            else if (result.MessageType == WebSocketMessageType.Close)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _cts.Token);
                _clients.Remove(ws);
            }
        }
    }
    private Task ProcessMessage(string json)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<WebSocketMessage>(json);
            if (msg?.type == "state_change")
            {
                var payload = JsonSerializer.Deserialize<StateChangePayload>(msg.payload.GetRawText());
                if (payload != null)
                {
                    StateChanged?.Invoke(this, payload);
                }
            }
        }
        catch
        {
            // Ignore invalid messages
        }
        return Task.CompletedTask;
    }
    public async Task StopAsync()
    {
        _cts.Cancel();
        foreach (var client in _clients)
        {
            if (client.State == WebSocketState.Open)
                await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        _listener.Stop();
    }
}
```

- [ ] **Step 2: 在MainWindow中集成WebSocket服务端**

修改 `MainWindow.xaml.cs`：

```csharp
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClaudeTrafficLight.Services;
namespace ClaudeTrafficLight;
public partial class MainWindow : Window
{
    private readonly WebSocketServer _wsServer;
    private readonly Dictionary<string, Brush> _stateColors = new()
    {
        ["idle"] = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
        ["thinking"] = new SolidColorBrush(Color.FromRgb(255, 200, 0)),
        ["writing"] = new SolidColorBrush(Color.FromRgb(0, 200, 83)),
        ["needs_confirm"] = new SolidColorBrush(Color.FromRgb(255, 68, 68))
    };
    public MainWindow()
    {
        InitializeComponent();
        MouseLeftButtonDown += (s, e) => DragMove();
        Loaded += async (s, e) =>
        {
            _wsServer = new WebSocketServer(19876);
            _wsServer.StateChanged += OnStateChanged;
            await _wsServer.StartAsync();
        };
        Closing += async (s, e) =>
        {
            await _wsServer.StopAsync();
        };
    }
    private void OnStateChanged(object? sender, StateChangePayload e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_stateColors.TryGetValue(e.state, out var color))
            {
                StatusLight.Fill = color;
            }
        });
    }
}
```

- [ ] **Step 3: 验证编译通过**

Run: `cd TrafficLightApp; dotnet build`
Expected: 编译成功

- [ ] **Step 4: 提交**

Run:
```
git add TrafficLightApp/Services/ TrafficLightApp/MainWindow.xaml.cs
git commit -m "feat: add WebSocket server and basic state-color mapping"
```

---

## 第三部分：VSCode扩展开发

### Task 4: VSCode扩展项目初始化

**Files:**
- Create: `vscode-extension/package.json`
- Create: `vscode-extension/tsconfig.json`
- Create: `vscode-extension/src/extension.ts`
- Create: `vscode-extension/.vscodeignore`

**Interfaces:**
- Produces: 可编译的VSCode扩展基础框架

- [ ] **Step 1: 创建package.json**

```json
{
  "name": "claude-traffic-light-connector",
  "displayName": "Claude Traffic Light Connector",
  "description": "Connect Claude Code status to desktop traffic light",
  "version": "0.1.0",
  "engines": { "vscode": "^1.80.0" },
  "categories": ["Other"],
  "activationEvents": ["*"],
  "main": "./out/extension.js",
  "contributes": {
    "commands": [{
      "command": "claude-traffic-light.injectHook",
      "title": "Claude Traffic Light: Inject Hook"
    },{
      "command": "claude-traffic-light.reinjectHook",
      "title": "Claude Traffic Light: Re-inject Hook"
    }]
  },
  "dependencies": { "ws": "^8.14.0" },
  "devDependencies": {
    "@types/vscode": "^1.80.0",
    "@types/node": "^18.0.0",
    "@types/ws": "^8.5.5",
    "typescript": "^5.0.0"
  },
  "scripts": { "vscode:prepublish": "npm run compile", "compile": "tsc -p ./" }
}
```

- [ ] **Step 2: 创建tsconfig.json**

```json
{
  "compilerOptions": {
    "module": "commonjs",
    "target": "ES2020",
    "outDir": "out",
    "lib": ["ES2020"],
    "sourceMap": true,
    "rootDir": "src",
    "strict": true
  },
  "exclude": ["node_modules", ".vscode-test"]
}
```

- [ ] **Step 3: 创建.vscodeignore**

```
.vscode/**
.vscode-test/**
src/**
tsconfig.json
npm-debug.log
yarn-error.log
**/*.map
node_modules/**
```

- [ ] **Step 4: 创建src/extension.ts（基础框架）**

```typescript
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import WebSocket from 'ws';
let ws: WebSocket | null = null;
function connectWebSocket() {
    try {
        ws = new WebSocket('ws://localhost:19876');
        ws.on('open', () => console.log('Traffic Light connected'));
        ws.on('error', () => setTimeout(connectWebSocket, 3000));
        ws.on('close', () => setTimeout(connectWebSocket, 3000));
    } catch {
        setTimeout(connectWebSocket, 3000);
    }
}
function sendState(state: string, message: string) {
    if (ws?.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({
            type: 'state_change',
            payload: {
                state,
                message,
                vscodeWindowId: vscode.env.sessionId,
                timestamp: Date.now()
            }
        }));
    }
}
export function activate(context: vscode.ExtensionContext) {
    connectWebSocket();
    context.subscriptions.push(
        vscode.commands.registerCommand('claude-traffic-light.injectHook', () => {
            vscode.window.showInformationMessage('Injecting Claude Code hook...');
        }),
        vscode.commands.registerCommand('claude-traffic-light.reinjectHook', () => {
            vscode.window.showInformationMessage('Re-injecting Claude Code hook...');
        })
    );
}
export function deactivate() {
    ws?.close();
}
```

- [ ] **Step 5: 安装依赖并编译**

Run: `cd vscode-extension; npm install; npm run compile`
Expected: 编译成功，生成out目录

- [ ] **Step 6: 提交**

Run:
```
git add vscode-extension/
git commit -m "feat: init VSCode extension with WebSocket client"
```
