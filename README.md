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
