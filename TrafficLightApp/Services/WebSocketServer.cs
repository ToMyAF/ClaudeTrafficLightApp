using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

public class ClientInfo
{
    public WebSocket WebSocket { get; set; } = null!;
    public string ClientId { get; set; } = string.Empty;
    public string EndPoint { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public string CurrentState { get; set; } = "idle";
    public string LastMessage { get; set; } = string.Empty;
    public DateTime? LastActivity { get; set; }
}

public class WebSocketServer
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<ClientInfo> _clients = new();

    public event EventHandler<StateChangePayload>? StateChanged;
    public event EventHandler<int>? ServerStarted;
    public event EventHandler<string>? ServerError;
    public event EventHandler<int>? ClientConnected;
    public event EventHandler<int>? ClientDisconnected;

    public int Port { get; }
    public int ClientCount => _clients.Count;
    public bool IsRunning { get; private set; }
    public IReadOnlyList<ClientInfo> Clients => _clients.AsReadOnly();

    public WebSocketServer(int port = 19876)
    {
        Port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public async Task StartAsync()
    {
        try
        {
            _listener.Start();
            IsRunning = true;
            ServerStarted?.Invoke(this, Port);
            _ = Task.Run(AcceptConnectionsLoop);
        }
        catch (HttpListenerException ex)
        {
            var errorMsg = $"端口 {Port} 被占用: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[WebSocketServer] {errorMsg}");
            ServerError?.Invoke(this, errorMsg);
        }
        catch (Exception ex)
        {
            var errorMsg = $"启动失败: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"[WebSocketServer] {errorMsg}");
            ServerError?.Invoke(this, errorMsg);
        }
    }

    private async Task AcceptConnectionsLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    var wsContext = await context.AcceptWebSocketAsync(null);
                    var clientInfo = new ClientInfo
                    {
                        WebSocket = wsContext.WebSocket,
                        ClientId = Guid.NewGuid().ToString("N")[..8],
                        EndPoint = context.Request.RemoteEndPoint?.ToString() ?? "未知",
                        ConnectedAt = DateTime.Now
                    };
                    _clients.Add(clientInfo);
                    ClientConnected?.Invoke(this, _clients.Count);
                    _ = Task.Run(() => HandleClient(clientInfo));
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WebSocketServer] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleClient(ClientInfo client)
    {
        var buffer = new byte[4096];
        try
        {
            while (client.WebSocket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                var result = await client.WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    client.LastActivity = DateTime.Now;
                    client.LastMessage = message;
                    await ProcessMessage(message, client);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", _cts.Token);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebSocketServer] Client error: {ex.Message}");
        }
        finally
        {
            _clients.Remove(client);
            client.WebSocket.Dispose();
            ClientDisconnected?.Invoke(this, _clients.Count);
        }
    }

    private Task ProcessMessage(string json, ClientInfo client)
    {
        try
        {
            var msg = JsonSerializer.Deserialize<WebSocketMessage>(json);
            if (msg?.type == "state_change")
            {
                var payload = JsonSerializer.Deserialize<StateChangePayload>(msg.payload.GetRawText());
                if (payload != null)
                {
                    client.CurrentState = payload.state;
                    if (!string.IsNullOrEmpty(payload.vscodeWindowId))
                    {
                        client.ClientId = payload.vscodeWindowId[..Math.Min(8, payload.vscodeWindowId.Length)];
                    }
                    StateChanged?.Invoke(this, payload);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebSocketServer] Error processing message: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        IsRunning = false;
        _cts.Cancel();
        foreach (var client in _clients.ToList())
        {
            try
            {
                if (client.WebSocket.State == WebSocketState.Open)
                    await client.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                client.WebSocket.Dispose();
            }
            catch { }
        }
        _clients.Clear();
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch { }
    }
}
