import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import WebSocket from 'ws';
import { HookInjector } from './HookInjector';

let ws: WebSocket | null = null;

function connectWebSocket() {
    try {
        ws = new WebSocket('ws://localhost:19876');
        ws.on('open', () => console.log('Traffic Light connected'));
        ws.on('error', () => {
            setTimeout(connectWebSocket, 3000);
        });
        ws.on('close', () => {
            setTimeout(connectWebSocket, 3000);
        });
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

// 监听来自网页的状态消息（通过自定义事件）
function setupWebviewMessageListener() {
    // 通过VSCode的扩展API监听状态变化
    // 这个函数会被注入的钩子代码调用
    (global as any).__claudeTrafficLightSendState = sendState;
}

export function activate(context: vscode.ExtensionContext) {
    connectWebSocket();
    setupWebviewMessageListener();
    
    // 检测Claude Code扩展
    const claudePath = HookInjector.findClaudeExtension();
    if (claudePath) {
        console.log(`Found Claude Code at: ${claudePath}`);
    }

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
        }),
        vscode.commands.registerCommand('claude-traffic-light.reinjectHook', async () => {
            // 重新注入逻辑
            vscode.window.showInformationMessage('重新注入钩子...');
        })
    );
}

export function deactivate() {
    ws?.close();
}
