using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using FiatLux.Models;
using FiatLux.Services;
using Microsoft.Maui.Dispatching;

namespace FiatLux.ViewModels;

public class RoomsViewModel : BindableObject
{
    private readonly WebSocketService _ws;

    public ObservableCollection<Room> Rooms { get; } = new();

    private string _debugMessage;
    public string DebugMessage
    {
        get => _debugMessage;
        set { _debugMessage = value; OnPropertyChanged(); }
    }

    private string _connectionStatus = "Connected";
    public string ConnectionStatus
    {
        get => _connectionStatus;
        set { _connectionStatus = value; OnPropertyChanged(); }
    }

    private bool _isConnected = true;
    public bool IsConnected
    {
        get => _isConnected;
        set { _isConnected = value; OnPropertyChanged(); }
    }

    public ICommand ReconnectCommand { get; }

    public RoomsViewModel(WebSocketService ws)
    {
        _ws = ws;
        _ws.MessageReceived += OnMessageReceived;
        _ws.ConnectionLost += OnConnectionLost;
        _ws.ConnectionEstablished += OnConnectionEstablished;

        ReconnectCommand = new Command(async () => await Reconnect());
    }

    private void OnConnectionEstablished()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = true;
            ConnectionStatus = "Connected";
            Debug.WriteLine("✅ Connexion établie");
        });
    }

    private void OnConnectionLost()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsConnected = false;
            ConnectionStatus = "Disconnected - Tap to reconnect";
            Debug.WriteLine("❌ Connexion perdue");
        });
    }

    private async Task Reconnect()
    {
        ConnectionStatus = "Reconnecting...";
        
        bool success = await _ws.ReconnectAsync();
        
        if (success)
        {
            ConnectionStatus = "Connected";
            IsConnected = true;
        }
        else
        {
            ConnectionStatus = "Connection failed - Tap to retry";
            IsConnected = false;
        }
    }

    private void OnMessageReceived(string json)
    {
        try
        {
            Debug.WriteLine("📦 Message reçu dans ViewModel");
            DebugMessage = json;

            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rooms", out var roomsElement))
            {
                Debug.WriteLine("⚠️ Pas de 'rooms' dans le JSON");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Rooms.Clear();

                foreach (var r in roomsElement.EnumerateArray())
                {
                    var room = new Room
                    {
                        RoomId = r.GetProperty("roomId").GetString(),
                        Lux = r.GetProperty("lux").GetDouble(),
                        LampPower = r.GetProperty("lampPower").GetInt32(),
                        TargetLux = r.GetProperty("targetLux").GetInt32(),
                        Mode = r.GetProperty("mode").GetString()
                    };

                    Rooms.Add(room);
                    Debug.WriteLine($"✅ Room {room.RoomId} ajoutée/maj dans UI");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur parsing JSON: {ex.Message}");
        }
    }
}
