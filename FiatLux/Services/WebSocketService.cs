using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Dispatching;
using System.Diagnostics;

namespace FiatLux.Services;

public class WebSocketService
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private string _currentUrl;

    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public event Action<string> MessageReceived;
    public event Action ConnectionEstablished;
    public event Action ConnectionLost;

    public async Task ConnectAsync(string url)
    {
        _currentUrl = url;
        
        if (_ws != null && _ws.State == WebSocketState.Open)
            return;

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            Debug.WriteLine($"✅ WebSocket connecté à {url}");
            ConnectionEstablished?.Invoke();
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur WebSocket: {ex.Message}");
            ConnectionLost?.Invoke();
            throw;
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];

        while (_ws.State == WebSocketState.Open)
        {
            try
            {
                var result = await _ws.ReceiveAsync(buffer, _cts.Token);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Debug.WriteLine("🔌 WebSocket fermé par le serveur");
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    ConnectionLost?.Invoke();
                }
                else
                {
                    var msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Debug.WriteLine($"📥 Reçu du serveur: {msg}");
                    MessageReceived?.Invoke(msg);
                }
            }
            catch (WebSocketException)
            {
                Debug.WriteLine("❌ Connexion WebSocket perdue");
                ConnectionLost?.Invoke();
                break;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("⚠️ Opération WebSocket annulée");
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Erreur réception WebSocket: {ex.Message}");
                ConnectionLost?.Invoke();
                break;
            }
        }

        // Si on sort de la boucle et que ce n'est pas une fermeture propre
        if (_ws.State != WebSocketState.Closed && _ws.State != WebSocketState.Aborted)
        {
            ConnectionLost?.Invoke();
        }
    }

    public async Task SendAsync(string message)
    {
        if (_ws?.State != WebSocketState.Open)
        {
            Debug.WriteLine("⚠️ WebSocket non connecté");
            return;
        }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            Debug.WriteLine($"📤 Envoyé: {message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur envoi: {ex.Message}");
            ConnectionLost?.Invoke();
        }
    }

    public async Task DisconnectAsync()
    {
        if (_ws != null)
        {
            _cts?.Cancel();
            
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Déconnecté", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ Erreur lors de la fermeture: {ex.Message}");
                }
            }
            
            _ws.Dispose();
            _ws = null;
        }
    }

    public async Task<bool> ReconnectAsync()
    {
        Debug.WriteLine("🔄 Tentative de reconnexion...");
        
        await DisconnectAsync();
        
        if (string.IsNullOrEmpty(_currentUrl))
        {
            Debug.WriteLine("❌ Pas d'URL enregistrée pour la reconnexion");
            return false;
        }

        try
        {
            await ConnectAsync(_currentUrl);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
