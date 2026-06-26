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
