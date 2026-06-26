import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';

export class HookInjector {
    private static readonly CLAUDE_EXT_ID = 'anthropic.claude-3-haiku-20240307';
    private static readonly HOOK_MARKER = '/* CLAUDE_TRAFFIC_LIGHT_HOOK */';
    
    static findClaudeExtension(): string | null {
        // 方法1: 通过VSCode API
        const extensions = vscode.extensions.all;
        const claudeExt = extensions.find(e => 
            e.id.toLowerCase().includes('anthropic') || 
            e.id.toLowerCase().includes('claude')
        );
        
        if (claudeExt) {
            return claudeExt.extensionPath;
        }
        
        // 方法2: 搜索常见路径
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

    static isHookInjected(jsPath: string): boolean {
        const content = fs.readFileSync(jsPath, 'utf8');
        return content.includes(this.HOOK_MARKER);
    }

    static async inject(jsPath: string): Promise<boolean> {
        try {
            // 先备份
            if (!fs.existsSync(jsPath + '.bak')) {
                fs.copyFileSync(jsPath, jsPath + '.bak');
            }
            
            let content = fs.readFileSync(jsPath, 'utf8');
            
            // 如果已经注入，先移除
            if (this.isHookInjected(jsPath)) {
                const markerIndex = content.indexOf(this.HOOK_MARKER);
                if (markerIndex > -1) {
                    content = content.substring(0, markerIndex);
                }
            }
            
            // 读取钩子代码
            const hookPath = path.join(__dirname, 'hook-code.js');
            const hookCode = fs.readFileSync(hookPath, 'utf8');
            
            // 在文件末尾注入
            content = content + '\n\n' + this.HOOK_MARKER + '\n' + hookCode;
            
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
}
