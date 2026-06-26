/* CLAUDE_TRAFFIC_LIGHT_HOOK_START */
(function() {
    if (window.__claudeTrafficLightInjected) return;
    window.__claudeTrafficLightInjected = true;
    
    console.log('[Claude Traffic Light] Hook injected!');
    
    // 发送状态给VSCode扩展
    function sendTrafficLightState(state, message) {
        try {
            // 通过VSCode的扩展API发送消息
            if (typeof acquireVsCodeApi !== 'undefined') {
                const vscode = acquireVsCodeApi();
                vscode.postMessage({
                    type: 'claude-traffic-light-state',
                    state: state,
                    message: message
                });
            }
            
            // 也通过自定义事件发送
            window.dispatchEvent(new CustomEvent('claude-traffic-light-state', {
                detail: { state, message }
            }));
        } catch(e) {}
    }
    
    // 监控DOM变化来检测状态
    const observer = new MutationObserver(function(mutations) {
        // 检查是否出现确认按钮
        const confirmButtons = Array.from(document.querySelectorAll('button'))
            .filter(btn => {
                const text = btn.textContent || '';
                return text.includes('Accept') || 
                       text.includes('Yes') || 
                       text.includes('Confirm') ||
                       text.includes('确认');
            });
        
        if (confirmButtons.length > 0) {
            sendTrafficLightState('needs_confirm', '需要确认操作');
            return;
        }
        
        // 检查思考状态（loading动画、"Thinking"文字等）
        const thinkingElements = document.querySelectorAll('[class*="loading"], [class*="thinking"], [class*="processing"]');
        const thinkingText = document.body.textContent?.includes('Thinking') || 
                            document.body.textContent?.includes('思考中') ||
                            document.body.textContent?.includes('Analyzing');
        
        if (thinkingElements.length > 0 || thinkingText) {
            sendTrafficLightState('thinking', '思考中...');
            return;
        }
        
        // 检查输出/写入状态
        const writingElements = document.querySelectorAll('[class*="writing"], [class*="typing"], [class*="streaming"]');
        if (writingElements.length > 0) {
            sendTrafficLightState('writing', '输出中...');
            return;
        }
        
        // 默认空闲状态
        sendTrafficLightState('idle', '空闲');
    });
    
    observer.observe(document.body, { 
        childList: true, 
        subtree: true,
        characterData: true
    });
    
    // 定期检查
    setInterval(() => {
        const hasConfirmBtn = Array.from(document.querySelectorAll('button'))
            .some(btn => {
                const text = btn.textContent || '';
                return text.includes('Accept') || text.includes('Yes') || text.includes('Confirm');
            });
        
        if (hasConfirmBtn) {
            sendTrafficLightState('needs_confirm', '需要确认操作');
        }
    }, 1000);
    
})();
/* CLAUDE_TRAFFIC_LIGHT_HOOK_END */
