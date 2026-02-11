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
    public ICommand AdminCommand { get; }
    public ICommand ExitAdminCommand { get; }

    public RoomsViewModel(WebSocketService ws)
    {
        _ws = ws;
        _ws.MessageReceived += OnMessageReceived;
        _ws.ConnectionLost += OnConnectionLost;
        _ws.ConnectionEstablished += OnConnectionEstablished;

        ReconnectCommand = new Command(async () => await Reconnect());
        AdminCommand = new Command(async () => await Shell.Current.GoToAsync("//adminlogin"));
        ExitAdminCommand = new Command(async () => await Shell.Current.GoToAsync("//rooms"));
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
                // Au lieu de Clear(), on met à jour les rooms existantes
                foreach (var r in roomsElement.EnumerateArray())
                {
                    var roomId = r.GetProperty("roomId").GetString();
                    
                    // Cherche si la room existe déjà
                    var existingRoom = Rooms.FirstOrDefault(room => room.RoomId == roomId);
                    
                    if (existingRoom != null)
                    {
                        // Met à jour la room existante
                        existingRoom.Lux = r.GetProperty("lux").GetDouble();
                        existingRoom.LampPower = r.GetProperty("lampPower").GetInt32();
                        existingRoom.TargetLux = r.GetProperty("targetLux").GetInt32();
                        existingRoom.Mode = r.GetProperty("mode").GetString();
                        
                        if (r.TryGetProperty("isManualMode", out var isManual))
                        {
                            existingRoom.IsManualMode = isManual.GetBoolean();
                        }
                        
                        // Met à jour les sensors si présents
                        if (r.TryGetProperty("sensors", out var sensorsElement))
                        {
                            existingRoom.Sensors.Clear();
                            foreach (var s in sensorsElement.EnumerateArray())
                            {
                                existingRoom.Sensors.Add(new Sensor
                                {
                                    SensorId = s.GetProperty("sensorId").GetString(),
                                    Type = s.GetProperty("type").GetString(),
                                    Value = s.GetProperty("value").GetDouble(),
                                    LastUpdate = s.GetProperty("lastUpdate").GetInt64(),
                                    Status = s.GetProperty("status").GetString()
                                });
                            }
                        }
                        
                        // Met à jour les lamps si présents
                        if (r.TryGetProperty("lamps", out var lampsElement))
                        {
                            existingRoom.Lamps.Clear();
                            foreach (var l in lampsElement.EnumerateArray())
                            {
                                existingRoom.Lamps.Add(new Lamp
                                {
                                    LampId = l.GetProperty("lampId").GetString(),
                                    Power = l.GetProperty("power").GetInt32(),
                                    Status = l.GetProperty("status").GetString(),
                                    LastUpdate = l.GetProperty("lastUpdate").GetInt64()
                                });
                            }
                        }
                    }
                    else
                    {
                        // Ajoute une nouvelle room
                        var newRoom = new Room
                        {
                            RoomId = roomId,
                            Lux = r.GetProperty("lux").GetDouble(),
                            LampPower = r.GetProperty("lampPower").GetInt32(),
                            TargetLux = r.GetProperty("targetLux").GetInt32(),
                            Mode = r.GetProperty("mode").GetString()
                        };
                        
                        if (r.TryGetProperty("isManualMode", out var isManual))
                        {
                            newRoom.IsManualMode = isManual.GetBoolean();
                        }
                        
                        // Ajoute les sensors si présents
                        if (r.TryGetProperty("sensors", out var sensorsElement))
                        {
                            foreach (var s in sensorsElement.EnumerateArray())
                            {
                                newRoom.Sensors.Add(new Sensor
                                {
                                    SensorId = s.GetProperty("sensorId").GetString(),
                                    Type = s.GetProperty("type").GetString(),
                                    Value = s.GetProperty("value").GetDouble(),
                                    LastUpdate = s.GetProperty("lastUpdate").GetInt64(),
                                    Status = s.GetProperty("status").GetString()
                                });
                            }
                        }
                        
                        // Ajoute les lamps si présents
                        if (r.TryGetProperty("lamps", out var lampsElement))
                        {
                            foreach (var l in lampsElement.EnumerateArray())
                            {
                                newRoom.Lamps.Add(new Lamp
                                {
                                    LampId = l.GetProperty("lampId").GetString(),
                                    Power = l.GetProperty("power").GetInt32(),
                                    Status = l.GetProperty("status").GetString(),
                                    LastUpdate = l.GetProperty("lastUpdate").GetInt64()
                                });
                            }
                        }
                        
                        Rooms.Add(newRoom);
                        Debug.WriteLine($"✅ Room {newRoom.RoomId} ajoutée");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur parsing JSON: {ex.Message}");
        }
    }
}
