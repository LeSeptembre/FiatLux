using System.Net.WebSockets;
using System.Text;

namespace FiatLux.Services;

public class WebSocketService
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public event Action<string>? MessageReceived;

    public async Task ConnectAsync(string uri)
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        await _ws.ConnectAsync(new Uri(uri), _cts.Token);

        _ = ReceiveLoop();
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[8192];

        while (_ws.State == WebSocketState.Open)
        {
            var result = await _ws.ReceiveAsync(buffer, _cts.Token);
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            MessageReceived?.Invoke(message);
        }
    }

    public async Task SendAsync(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(
            bytes,
            WebSocketMessageType.Text,
            true,
            _cts.Token
        );
    }

    public async Task DisconnectAsync()
    {
        if (_ws?.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Client disconnect",
                CancellationToken.None
            );
        }
    }
}
