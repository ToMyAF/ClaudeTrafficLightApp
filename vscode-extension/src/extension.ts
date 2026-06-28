import * as vscode from 'vscode';
import * as path from 'path';
import * as fs from 'fs';
import WebSocket = require('ws');

let ws: WebSocket | null = null;
let reconnectAttempts = 0;
const maxReconnectDelay = 60000; // 最大延迟1分钟
let lastSentState: string | null = null;
let filePollingInterval: NodeJS.Timeout | null = null;

// 状态文件路径
const STATE_FILE = path.join(require('os').homedir(), '.claude', 'cc_traffic_light_state');

// 日志前缀
const LOG_PREFIX = '[Claude Traffic Light]';

// 获取 WebSocket 地址
function getWebSocketUrl(): string {
    const config = vscode.workspace.getConfiguration('claudeTrafficLight');
    const host = config.get<string>('websocket.host', 'localhost');
    const port = config.get<number>('websocket.port', 19876);
    return `ws://${host}:${port}`;
}

// 获取最大重连次数
function getMaxReconnectAttempts(): number {
    const config = vscode.workspace.getConfiguration('claudeTrafficLight');
    return config.get<number>('websocket.maxReconnectAttempts', 10);
}

// 控制台日志
function log(message: string, level: 'info' | 'error' | 'debug' = 'info') {
    const config = vscode.workspace.getConfiguration('claudeTrafficLight');
    if (level === 'debug' && !config.get<boolean>('showStateChanges', false)) {
        return;
    }

    const prefix = level === 'error' ? `${LOG_PREFIX} ❌` : level === 'debug' ? `${LOG_PREFIX} 🔍` : `${LOG_PREFIX} ℹ️`;
    const logFn = level === 'error' ? console.error : console.log;
    logFn(`${prefix} ${message}`);
}

function connectWebSocket() {
    const url = getWebSocketUrl();
    log(`正在连接 WebSocket: ${url}`);

    try {
        ws = new WebSocket(url);

        ws.on('open', () => {
            log(`WebSocket 连接成功: ${url}`);
            vscode.window.showInformationMessage('🚦 Claude Traffic Light 连接成功！信号灯已激活');
            reconnectAttempts = 0; // 连接成功后重置计数器
        });

        ws.on('error', (err: Error) => {
            log(`WebSocket 错误: ${err.message}`, 'error');
            handleReconnect();
        });

        ws.on('close', (code: number, reason: string) => {
            log(`WebSocket 断开 (code: ${code}, reason: ${reason || '未知'})`, 'error');
            handleReconnect();
        });
    } catch (err: any) {
        log(`WebSocket 连接失败: ${err.message}`, 'error');
        handleReconnect();
    }
}

// 处理重连逻辑
function handleReconnect() {
    const maxAttempts = getMaxReconnectAttempts();

    if (maxAttempts > 0 && reconnectAttempts >= maxAttempts) {
        log(`已达到最大重连次数 (${maxAttempts})，停止重连`, 'error');
        vscode.window.showWarningMessage(
            `⚠️ Claude Traffic Light 连接失败，已达到最大重连次数 (${maxAttempts})，停止重连。可通过命令重新连接。`,
            '重新连接'
        ).then(selection => {
            if (selection === '重新连接') {
                reconnectAttempts = 0;
                connectWebSocket();
            }
        });
        return;
    }

    const delay = Math.min(1000 * Math.pow(2, reconnectAttempts), maxReconnectDelay);
    const nextAttempt = reconnectAttempts + 1;
    const remaining = maxAttempts > 0 ? ` (${nextAttempt}/${maxAttempts})` : '';
    log(`${Math.round(delay / 1000)} 秒后尝试重连${remaining}...`);
    reconnectAttempts++;
    setTimeout(connectWebSocket, delay);
}

function sendState(state: string, message: string) {
    if (ws?.readyState === WebSocket.OPEN) {
        const payload = {
            type: 'state_change',
            payload: {
                state,
                message,
                vscodeWindowId: vscode.env.sessionId,
                timestamp: Date.now()
            }
        };
        ws.send(JSON.stringify(payload));

        // 状态变化时输出日志
        if (state !== lastSentState) {
            log(`状态变化: ${lastSentState || '初始'} → ${state}`, 'debug');
            lastSentState = state;
        }
    } else {
        log(`无法发送状态: WebSocket 未连接 (readyState: ${ws?.readyState})`, 'debug');
    }
}

// 监听配置变更
function setupConfigurationListener(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.workspace.onDidChangeConfiguration(e => {
            if (e.affectsConfiguration('claudeTrafficLight.websocket')) {
                log('WebSocket 配置已变更，将重新连接');
                ws?.close();
                reconnectAttempts = 0;
                connectWebSocket();
            }
            if (e.affectsConfiguration('claudeTrafficLight.showStateChanges')) {
                log('调试日志配置已变更');
            }
        })
    );
}

// 显示当前状态
function showStatus() {
    const url = getWebSocketUrl();
    const wsState = ws?.readyState;
    let stateText = '未知';
    switch (wsState) {
        case WebSocket.CONNECTING: stateText = '连接中...'; break;
        case WebSocket.OPEN: stateText = '已连接'; break;
        case WebSocket.CLOSING: stateText = '关闭中...'; break;
        case WebSocket.CLOSED: stateText = '已关闭'; break;
    }

    const maxAttempts = getMaxReconnectAttempts();
    const maxAttemptsText = maxAttempts > 0 ? `${maxAttempts} 次` : '无限';
    const message =
        `${LOG_PREFIX}\n\n` +
        `🔌 WebSocket 地址: ${url}\n` +
        `📡 连接状态: ${stateText}\n` +
        `🔄 重连尝试次数: ${reconnectAttempts} / ${maxAttemptsText}\n` +
        `🎯 当前状态: ${lastSentState || '未发送'}\n` +
        `📄 状态文件: ${STATE_FILE}\n\n` +
        `✅ 监控模式: 文件轮询（500ms）\n\n` +
        `💡 状态对应：\n` +
        `   🟡 黄灯 = 思考/处理中\n` +
        `   🟢 绿灯 = 输出结果中\n` +
        `   🔴 红灯 = 需要用户确认\n` +
        `   ⚪ 熄灭 = 空闲`;

    log('用户查询状态');
    vscode.window.showInformationMessage(message, { modal: true });
}

// 显示欢迎信息
function showWelcome() {
    vscode.window.showInformationMessage(
        `${LOG_PREFIX} 扩展已激活！✅ 文件轮询监控已启动`,
        '查看状态',
        '查看说明'
    ).then(selection => {
        if (selection === '查看状态') {
            showStatus();
        } else if (selection === '查看说明') {
            vscode.window.showInformationMessage(
                `${LOG_PREFIX} 💡 使用说明：\n\n` +
                `✅ 采用文件轮询方案（稳定可靠）\n\n` +
                `Claude Code Hooks 会写入状态到文件：\n` +
                `• 🟡 黄灯 = yellow = 思考/处理中\n` +
                `• 🟢 绿灯 = green = 输出结果中\n` +
                `• 🔴 红灯 = red = 需要用户确认\n\n` +
                `也可手动切换：Ctrl+Alt+C 打开快速切换面板`
            );
        }
    });
}

// ========================================================================
// 手动控制命令（作为 WebView 注入的替代方案）
// ========================================================================

function registerManualCommands(context: vscode.ExtensionContext) {
    context.subscriptions.push(
        vscode.commands.registerCommand('claude-traffic-light.setThinking', () => {
            log('手动设置状态: thinking');
            sendState('thinking', '思考中...');
            vscode.window.showInformationMessage(`${LOG_PREFIX} 🟡 已设置为思考状态`);
        }),
        vscode.commands.registerCommand('claude-traffic-light.setWriting', () => {
            log('手动设置状态: writing');
            sendState('writing', '输出中...');
            vscode.window.showInformationMessage(`${LOG_PREFIX} 🟢 已设置为输出状态`);
        }),
        vscode.commands.registerCommand('claude-traffic-light.setConfirm', () => {
            log('手动设置状态: needs_confirm');
            sendState('needs_confirm', '需要确认操作');
            vscode.window.showInformationMessage(`${LOG_PREFIX} 🔴 已设置为需要确认状态`);
        }),
        vscode.commands.registerCommand('claude-traffic-light.setIdle', () => {
            log('手动设置状态: idle');
            sendState('idle', '空闲');
            vscode.window.showInformationMessage(`${LOG_PREFIX} ⚪ 已设置为空闲状态`);
        }),
        // 快速切换命令面板
        vscode.commands.registerCommand('claude-traffic-light.quickSwitch', async () => {
            const selection = await vscode.window.showQuickPick([
                { label: '🟡 思考中', description: 'thinking', value: 'thinking' },
                { label: '🟢 输出中', description: 'writing', value: 'writing' },
                { label: '🔴 需要确认', description: 'needs_confirm', value: 'needs_confirm' },
                { label: '⚪ 空闲', description: 'idle', value: 'idle' }
            ], {
                title: '选择 Claude Traffic Light 状态',
                placeHolder: '请选择要设置的状态...'
            });

            if (selection) {
                const messages: Record<string, string> = {
                    'thinking': '思考中...',
                    'writing': '输出中...',
                    'needs_confirm': '需要确认操作',
                    'idle': '空闲'
                };
                sendState(selection.value, messages[selection.value]);
                log(`快速切换状态: ${selection.value}`);
            }
        })
    );
}

// 颜色到状态的映射
const COLOR_TO_STATE: Record<string, { state: string; message: string }> = {
    'red': { state: 'needs_confirm', message: '需要确认操作' },
    'green': { state: 'writing', message: '输出结果中' },
    'yellow': { state: 'thinking', message: '思考处理中' },
};

let lastFileState: string | null = null;

// 读取状态文件
function readStateFile(): string | null {
    try {
        if (fs.existsSync(STATE_FILE)) {
            const content = fs.readFileSync(STATE_FILE, 'utf-8').trim().toLowerCase();
            return content;
        }
    } catch (e) {
        // 静默失败
    }
    return null;
}

// 文件轮询检查
function checkStateFile() {
    const state = readStateFile();

    if (state && state !== lastFileState && COLOR_TO_STATE[state]) {
        const { state: newState, message } = COLOR_TO_STATE[state];
        lastFileState = state;
        log(`检测到状态变化: ${state} -> ${newState}`, 'debug');
        sendState(newState, message);
    }
}

// 启动文件轮询
function startFilePolling() {
    if (filePollingInterval) {
        clearInterval(filePollingInterval);
    }

    // 每500ms检查一次状态文件
    filePollingInterval = setInterval(checkStateFile, 500);
    log('已启动状态文件轮询监控');

    // 初始检查一次
    checkStateFile();
}

// 停止文件轮询
function stopFilePolling() {
    if (filePollingInterval) {
        clearInterval(filePollingInterval);
        filePollingInterval = null;
    }
}

export function activate(context: vscode.ExtensionContext) {
    log('扩展正在激活...');

    connectWebSocket();
    setupConfigurationListener(context);
    registerManualCommands(context);

    // 启动文件轮询
    startFilePolling();

    // 注册命令
    context.subscriptions.push(
        vscode.commands.registerCommand('claude-traffic-light.showStatus', () => {
            showStatus();
        }),
        vscode.commands.registerCommand('claude-traffic-light.reconnect', () => {
            log('用户手动重连');
            ws?.close();
            reconnectAttempts = 0;
            connectWebSocket();
        })
    );

    // 延迟显示欢迎信息
    setTimeout(showWelcome, 2000);

    log('扩展激活完成');
}

export function deactivate() {
    log('扩展正在关闭...');
    stopFilePolling();
    ws?.close();
    log('扩展已关闭');
}
