using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Diagnostics;

namespace FiatLux.Services;

public class WebSocketService
{
    private ClientWebSocket _ws;
    private CancellationTokenSource _cts;
    private string _currentUrl;
    private string _sessionId;

    public bool IsConnected => _ws?.State == WebSocketState.Open;
    public string AdminToken { get; private set; }
    public string SessionId => _sessionId;
    public bool IsAdmin => !string.IsNullOrEmpty(AdminToken);

    public event Action<string> MessageReceived;
    public event Action ConnectionEstablished;
    public event Action ConnectionLost;
    public event Action<bool, string> AdminAuthResponse;

    public WebSocketService()
    {
        // Génère un session ID unique au démarrage de l'app
        _sessionId = $"maui_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        Debug.WriteLine($"🆔 Session ID: {_sessionId}");
    }

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
            Debug.WriteLine($"🆔 Session: {_sessionId}");
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

                    ParseAdminResponse(msg);
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

    public async Task RequestAdminAsync(string password)
    {
        var payload = new
        {
            command = "requestAdmin",
            password,
            sessionId = _sessionId // ✅ Envoie le session ID
        };

        await SendAsync(JsonSerializer.Serialize(payload));
        Debug.WriteLine($"🔑 Demande admin avec session: {_sessionId}");
    }

    public async Task SetManualModeAsync(string roomId, bool enabled)
    {
        if (!IsAdmin)
        {
            Debug.WriteLine("❌ Pas de token admin");
            return;
        }

        var payload = new
        {
            command = "setManualMode",
            roomId,
            enabled,
            adminToken = AdminToken,
            sessionId = _sessionId // ✅ Envoie le session ID
        };

        await SendAsync(JsonSerializer.Serialize(payload));
        Debug.WriteLine($"🔧 Mode manuel [Session: {_sessionId}]");
    }

    public async Task SetManualTargetAsync(string roomId, int target)
    {
        if (!IsAdmin)
        {
            Debug.WriteLine("❌ Pas de token admin");
            return;
        }

        var payload = new
        {
            command = "setManualTarget",
            roomId,
            target,
            adminToken = AdminToken,
            sessionId = _sessionId // ✅ Envoie le session ID
        };

        await SendAsync(JsonSerializer.Serialize(payload));
        Debug.WriteLine($"🎯 Target manuel [Session: {_sessionId}]");
    }

    private void ParseAdminResponse(string message)
    {
        try
        {
            if (message.Contains("\"adminToken\"") || message.Contains("\"success\""))
            {
                var doc = JsonDocument.Parse(message);

                if (doc.RootElement.TryGetProperty("success", out var success))
                {
                    bool isSuccess = success.GetBoolean();

                    if (isSuccess && doc.RootElement.TryGetProperty("adminToken", out var token))
                    {
                        AdminToken = token.GetString();
                        Debug.WriteLine($"✅ Token admin reçu: {AdminToken}");
                        Debug.WriteLine($"🆔 Session: {_sessionId}");
                        AdminAuthResponse?.Invoke(true, AdminToken);
                    }
                    else if (!isSuccess && doc.RootElement.TryGetProperty("error", out var error))
                    {
                        string errorMsg = error.GetString();
                        Debug.WriteLine($"❌ Authentification échouée: {errorMsg}");
                        AdminAuthResponse?.Invoke(false, errorMsg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Erreur parsing réponse admin: {ex.Message}");
        }
    }

    public void ClearAdminToken()
    {
        AdminToken = null;
        Debug.WriteLine("🔓 Token admin effacé");
    }

    public async Task DisconnectAsync()
    {
        if (_ws != null)
        {
            _cts?.Cancel();
            AdminToken = null;

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
        Debug.WriteLine($"🆔 Même session: {_sessionId}");

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