### Task 5: Claude Code钩子注入器（文件操作部分）

**Files:**
- Create: `vscode-extension/src/HookInjector.ts`
- Modify: `vscode-extension/src/extension.ts`

**Interfaces:**
- Consumes: VSCode file system API
- Produces: `HookInjector.findClaudeExtension()`, `HookInjector.inject()`

- [ ] **Step 1: 创建HookInjector.ts基础类**

```typescript
import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
export class HookInjector {
    private static readonly CLAUDE_EXT_ID = 'anthropic.claude-3-haiku-20240307';
    private static readonly HOOK_MARKER = '/* CLAUDE_TRAFFIC_LIGHT_HOOK */';
    
    static findClaudeExtension(): string | null {
        const extensions = vscode.extensions.all;
        const claudeExt = extensions.find(e => 
            e.id.toLowerCase().includes('anthropic') || 
            e.id.toLowerCase().includes('claude')
        );
        if (claudeExt) {
            return claudeExt.extensionPath;
        }
        // Fallback: search common paths
        const vscodeExtensionsPath = path.join(
            process.env.USERPROFILE || '',
            '.vscode', 'extensions'
        );
        if (fs.existsSync(vscodeExtensionsPath)) {
            const dirs = fs.readdirSync(vscodeExtensionsPath);
            const claudeDir = dirs.find(d => 
                d.toLowerCase().includes('anthropic') || 
                d.toLowerCase().includes('claude')
            );
            if (claudeDir) {
                return path.join(vscodeExtensionsPath, claudeDir);
            }
        }
        return null;
    }
    static findExtensionJs(extPath: string): string | null {
        const possiblePaths = [
            path.join(extPath, 'dist', 'extension.js'),
            path.join(extPath, 'out', 'extension.js'),
            path.join(extPath, 'extension.js'),
            path.join(extPath, 'main.js')
        ];
        return possiblePaths.find(p => fs.existsSync(p)) || null;
    }
}
```

- [ ] **Step 2: 在extension.ts中集成HookInjector**

在 `extension.ts` 顶部添加：
```typescript
import { HookInjector } from './HookInjector';
```

在 activate 函数中添加：
```typescript
const claudePath = HookInjector.findClaudeExtension();
if (claudePath) {
    vscode.window.showInformationMessage(`Found Claude Code at: ${claudePath}`);
} else {
    vscode.window.showWarningMessage('Claude Code extension not found');
}
```

- [ ] **Step 3: 重新编译测试**

Run: `cd vscode-extension; npm run compile`
Expected: 编译成功

- [ ] **Step 4: 提交**

Run:
```
git add vscode-extension/src/HookInjector.ts vscode-extension/src/extension.ts
git commit -m "feat: add Claude Code extension detection"
```

---
