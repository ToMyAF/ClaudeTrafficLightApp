## 第四部分：钩子注入与状态识别（难点）

### Task 8: Claude Code钩子注入实现

**Files:**
- Modify: `vscode-extension/src/HookInjector.ts`
- Create: `vscode-extension/src/hook-code.js`

**Interfaces:**
- Produces: 注入钩子代码到Claude Code的extension.js

**注意：这是最关键的任务，需要实际分析你机器上的Claude Code扩展代码**

- [ ] **Step 1: 创建钩子注入代码模板**

创建 `vscode-extension/src/hook-code.js`：

```javascript
/* CLAUDE_TRAFFIC_LIGHT_HOOK_START */
(function() {
    if (window.__claudeTrafficLightInjected) return;
    window.__claudeTrafficLightInjected = true;
    
    // 通过VSCode API发送消息给我们的扩展
    function sendTrafficLightState(state, message) {
        try {
            window.dispatchEvent(new CustomEvent('claude-traffic-light-state', {
                detail: { state, message }
            }));
        } catch(e) {}
    }
    
    // 监控DOM变化来检测确认按钮
    const observer = new MutationObserver(function(mutations) {
        mutations.forEach(function(mutation) {
            // 检查是否出现Accept按钮
            const acceptBtns = document.querySelectorAll('button');
            for (let btn of acceptBtns) {
                if (btn.textContent && btn.textContent.includes('Accept') || 
                    btn.textContent?.includes('Yes') ||
                    btn.textContent?.includes('Confirm')) {
                    sendTrafficLightState('needs_confirm', '需要确认操作');
                    return;
                }
            }
            // 检查思考状态
            const thinkingElements = document.querySelectorAll('[class*="thinking"], [class*="processing"]');
            if (thinkingElements.length > 0) {
                sendTrafficLightState('thinking', '思考中...');
            }
        });
    });
    
    observer.observe(document.body, { childList: true, subtree: true });
})();
/* CLAUDE_TRAFFIC_LIGHT_HOOK_END */
```

- [ ] **Step 2: 实现注入逻辑**

在 `HookInjector.ts` 中添加：

```typescript
static isHookInjected(jsPath: string): boolean {
    const content = fs.readFileSync(jsPath, 'utf8');
    return content.includes(this.HOOK_MARKER);
}
static async inject(jsPath: string): Promise<boolean> {
    try {
        if (this.isHookInjected(jsPath)) {
            this.removeHook(jsPath);
        }
        let content = fs.readFileSync(jsPath, 'utf8');
        // 在文件末尾注入DOM监控代码
        const hookCode = fs.readFileSync(
            path.join(__dirname, '..', 'src', 'hook-code.js'),
            'utf8'
        );
        content = content + '\n' + hookCode;
        // 创建备份
        fs.copyFileSync(jsPath, jsPath + '.bak');
        fs.writeFileSync(jsPath, content);
        return true;
    } catch (e) {
        console.error('Inject failed:', e);
        return false;
    }
}
static removeHook(jsPath: string): boolean {
    try {
        const backupPath = jsPath + '.bak';
        if (fs.existsSync(backupPath)) {
            fs.copyFileSync(backupPath, jsPath);
            return true;
        }
        return false;
    } catch {
        return false;
    }
}
```

- [ ] **Step 3: 在extension.ts中使用注入器**

```typescript
// 监听来自网页的状态消息
export function activate(context: vscode.ExtensionContext) {
    connectWebSocket();
    
    // 注入钩子命令
    context.subscriptions.push(
        vscode.commands.registerCommand('claude-traffic-light.injectHook', async () => {
            const extPath = HookInjector.findClaudeExtension();
            if (!extPath) {
                vscode.window.showErrorMessage('Claude Code扩展未找到');
                return;
            }
            const jsPath = HookInjector.findExtensionJs(extPath);
            if (!jsPath) {
                vscode.window.showErrorMessage('未找到extension.js');
                return;
            }
            const success = await HookInjector.inject(jsPath);
            if (success) {
                vscode.window.showInformationMessage('钩子注入成功！请重启VSCode。');
            } else {
                vscode.window.showErrorMessage('钩子注入失败');
            }
        })
    );
}
```

- [ ] **Step 4: 复制hook-code.js到out目录**

在package.json的scripts中添加：
```json
"copy-hook": "copy src\\hook-code.js out\\",
"compile": "tsc -p ./ && npm run copy-hook"
```

- [ ] **Step 5: 编译测试**

Run: `cd vscode-extension; npm run compile`
Expected: 编译成功

- [ ] **Step 6: 提交**

Run:
```
git add vscode-extension/src/HookInjector.ts vscode-extension/src/hook-code.js vscode-extension/package.json
git commit -m "feat: implement hook injection for Claude Code"
```

---

### Task 9: 打包与发布准备

**Files:**
- Modify: `vscode-extension/package.json`
- Create: `TrafficLightApp/publish.bat`

**Interfaces:**
- Produces: .vsix扩展包、可执行的WPF程序

- [ ] **Step 1: 安装vsce打包工具**

Run: `npm install -g @vscode/vsce`

- [ ] **Step 2: 打包VSCode扩展**

Run: `cd vscode-extension; vsce package`
Expected: 生成 claude-traffic-light-connector-0.1.0.vsix

- [ ] **Step 3: 发布WPF程序**

创建 `TrafficLightApp/publish.bat`：

```batch
@echo off
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
echo Publish complete!
```

Run: `cd TrafficLightApp; publish.bat`
Expected: 生成 ClaudeTrafficLight.exe

- [ ] **Step 4: 提交**

Run:
```
git add TrafficLightApp/publish.bat
git commit -m "feat: add publish scripts"
```

---

## 验收测试任务

### Task 10: 端到端测试

- [ ] **Step 1: 运行红绿灯程序**
Run: `TrafficLightApp/publish/ClaudeTrafficLight.exe`
Expected: 窗口显示，灰色灯+等待连接消息

- [ ] **Step 2: 安装VSCode扩展**
Run: `code --install-extension vscode-extension/claude-traffic-light-connector-0.1.0.vsix`

- [ ] **Step 3: 注入钩子**
- 在VSCode中按Ctrl+Shift+P
- 运行 "Claude Traffic Light: Inject Hook"
- 重启VSCode

- [ ] **Step 4: 测试状态变化**
1. 打开Claude Code
2. 发送一个消息给Claude
3. 观察：思考时亮黄灯
4. 观察：输出时亮绿灯
5. 观察：出现Accept确认时亮红灯闪烁

- [ ] **Step 5: 测试多窗口**
- 打开第二个VSCode窗口
- 观察红绿灯是否轮播不同窗口的状态

---

## 已知风险和注意事项

1. **Claude Code更新**：每次Claude Code自动更新后，钩子会被覆盖，需要重新运行注入命令
2. **DOM选择器失效**：如果Claude Code修改了UI class名称，需要更新hook-code.js中的选择器
3. **VSCode安全限制**：某些VSCode版本可能限制修改扩展文件，可能需要以管理员身份运行
