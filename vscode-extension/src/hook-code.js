/* CLAUDE_TRAFFIC_LIGHT_HOOK_START */
// ============================================================================
// Claude Traffic Light WebView 拦截器
// 目标：拦截 Claude Code 创建 WebView 的过程，注入监控脚本
// ============================================================================
(function() {
    // 终极安全保护
    try {
        // 防止重复注入
        if (globalThis.__claudeTrafficLightHooked) {
            return;
        }
        globalThis.__claudeTrafficLightHooked = true;

        console.log('[Claude Traffic Light] 🚦 钩子已激活');

        // ====================================================================
        // 全局状态
        // ====================================================================
        let currentState = 'idle';
        let sendStateFn = null;

        // 尝试从全局获取发送状态的函数
        function tryGetSendStateFn() {
            try {
                if (globalThis.__claudeTrafficLightSendState) {
                    sendStateFn = globalThis.__claudeTrafficLightSendState;
                    return true;
                }
                // @ts-ignore
                if (global.__claudeTrafficLightSendState) {
                    // @ts-ignore
                    sendStateFn = global.__claudeTrafficLightSendState;
                    return true;
                }
            } catch (e) {}
            return false;
        }

        function sendState(state, message) {
            try {
                if (state === currentState) return;
                currentState = state;

                // 尝试获取发送函数
                if (!sendStateFn) {
                    tryGetSendStateFn();
                }

                if (sendStateFn) {
                    sendStateFn(state, message);
                    console.log(`[Claude Traffic Light] 📤 状态已发送: ${state}`);
                } else {
                    // 如果还没有发送函数，暂时用日志输出
                    console.log(`[Claude Traffic Light] 🎯 状态变化: ${state} - ${message}`);
                }
            } catch (e) {
                // 静默失败
            }
        }

        // ====================================================================
        // 监控脚本 - 这会被注入到 WebView HTML 中
        // ====================================================================
        const MONITOR_SCRIPT = `
<script>
(function() {
    if (window.__claudeTrafficLightInjected) return;
    window.__claudeTrafficLightInjected = true;

    console.log('[Claude Traffic Light] 🔍 WebView 监控已激活');

    let currentState = 'idle';
    let observer = null;
    let checkInterval = null;

    // 发送状态到扩展主进程
    function sendState(state, message) {
        try {
            if (typeof acquireVsCodeApi !== 'undefined') {
                const vscode = acquireVsCodeApi();
                vscode.postMessage({
                    type: 'claude-traffic-light-state',
                    state: state,
                    message: message
                });
            }
        } catch(e) {
            // 静默失败
        }
    }

    // 检查当前状态
    function checkState() {
        try {
            const bodyText = document.body.textContent || '';

            // 1. 检测需要确认的状态
            const buttons = document.querySelectorAll('button');
            let hasConfirm = false;
            for (let i = 0; i < buttons.length; i++) {
                const text = buttons[i].textContent || '';
                if (text.includes('Accept') ||
                    text.includes('Yes') ||
                    text.includes('Confirm') ||
                    text.includes('确认') ||
                    text.includes('Approve')) {
                    hasConfirm = true;
                    break;
                }
            }

            // 2. 检测思考中
            const isThinking = bodyText.includes('Thinking') ||
                              bodyText.includes('思考中') ||
                              bodyText.includes('Analyzing') ||
                              document.querySelectorAll('[class*="loading"], [class*="thinking"], [class*="processing"], [aria-busy="true"]').length > 0;

            // 3. 检测输出中（打字动画）
            const isWriting = document.querySelectorAll('[class*="typing"], [class*="streaming"], [class*="cursor"]').length > 0;

            let newState = 'idle';
            let newMessage = '空闲';

            if (hasConfirm) {
                newState = 'needs_confirm';
                newMessage = '需要确认操作';
            } else if (isThinking) {
                newState = 'thinking';
                newMessage = '思考中...';
            } else if (isWriting) {
                newState = 'writing';
                newMessage = '输出中...';
            }

            if (newState !== currentState) {
                currentState = newState;
                sendState(newState, newMessage);
            }
        } catch (e) {
            // 静默失败
        }
    }

    // 设置 DOM 变化监听器
    try {
        observer = new MutationObserver(function(mutations) {
            if (!window.__claudeTrafficLightThrottle) {
                window.__claudeTrafficLightThrottle = setTimeout(function() {
                    window.__claudeTrafficLightThrottle = null;
                    checkState();
                }, 250);
            }
        });

        observer.observe(document.body || document.documentElement, {
            childList: true,
            subtree: true,
            characterData: true,
            attributes: false
        });
    } catch (e) {
        console.log('[Claude Traffic Light] MutationObserver 不可用');
    }

    // 定期检查作为后备
    checkInterval = setInterval(checkState, 2000);

    // 首次检查
    setTimeout(checkState, 1000);

    // 清理函数
    window.__claudeTrafficLightCleanup = function() {
        try {
            if (observer) observer.disconnect();
            if (checkInterval) clearInterval(checkInterval);
        } catch (e) {}
    };
})();
</script>
`;

        // ====================================================================
        // 拦截 WebView HTML 设置
        // ====================================================================
        function injectHtmlScript(html) {
            try {
                // 如果已经注入过了，就不再注入
                if (html.includes('claude-traffic-light-state')) {
                    return html;
                }

                // 在 </body> 之前插入脚本
                if (html.includes('</body>')) {
                    html = html.replace('</body>', MONITOR_SCRIPT + '</body>');
                } else if (html.includes('</html>')) {
                    html = html.replace('</html>', MONITOR_SCRIPT + '</html>');
                } else {
                    html = html + MONITOR_SCRIPT;
                }

                console.log('[Claude Traffic Light] ✅ WebView HTML 已注入监控脚本');
                return html;
            } catch (e) {
                console.log('[Claude Traffic Light] ❌ 注入失败:', e.message);
                return html;
            }
        }

        // ====================================================================
        // 尝试拦截 VSCode API
        // ====================================================================

        // 方法1: 直接等待 VSCode API 可用
        function hookVscodeAPI() {
            try {
                // @ts-ignore
                if (typeof acquireVsCodeApi !== 'undefined') {
                    console.log('[Claude Traffic Light] ✅ acquireVsCodeApi 已可用');
                    sendState('idle', '监控已激活');
                }
            } catch (e) {
                // 静默失败
            }
        }

        // ====================================================================
        // 注意：在 Node.js extension.js 环境中，我们需要拦截 webview.html 的设置
        // 但是，VSCode 扩展环境中，我们无法直接拦截其他扩展的 WebView
        // 所以，这个脚本主要是提供给 WebView 环境中运行使用的
        // 实际上，当 Claude Code 的 WebView 创建时，我们的脚本需要被注入到那里
        //
        // 由于架构限制，我们使用一个更简单但有效的方案：
        // 1. 我们的扩展提供手动切换状态的命令
        // 2. 用户可以通过命令面板快速切换信号灯状态
        // 3. 这是一个 100% 安全且可靠的方案
        // ====================================================================

        console.log('[Claude Traffic Light] ✅ 钩子初始化完成（手动模式）');

    } catch (e) {
        // 终极错误捕获：确保不会影响 Claude Code 的任何功能
        try {
            console.log('[Claude Traffic Light] ⚠️ 钩子错误（已安全捕获）:', e.message);
        } catch {
            // 即使 console.log 也失败了，我们也什么都不做
        }
    }
})();
/* CLAUDE_TRAFFIC_LIGHT_HOOK_END */
