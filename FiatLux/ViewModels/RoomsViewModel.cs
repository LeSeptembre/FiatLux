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
            Debug.WriteLine("📦 Message reçu dans RoomsViewModel");
            DebugMessage = json;

            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rooms", out var roomsElement))
            {
                Debug.WriteLine("⚠️ Pas de 'rooms' dans le JSON");
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                foreach (var r in roomsElement.EnumerateArray())
                {
                    if (!r.TryGetProperty("roomId", out var roomIdEl))
                    {
                        Debug.WriteLine("⚠️ Pas de 'roomId' dans une room");
                        continue;
                    }

                    var roomId = roomIdEl.GetString();
                    var existingRoom = Rooms.FirstOrDefault(room => room.RoomId == roomId);

                    if (existingRoom != null)
                    {
                        // ✅ FIXÉ - Met à jour uniquement les propriétés présentes
                        if (r.TryGetProperty("lux", out var luxEl))
                            existingRoom.Lux = luxEl.GetDouble();

                        if (r.TryGetProperty("lampPower", out var powerEl))
                            existingRoom.LampPower = powerEl.GetInt32();

                        if (r.TryGetProperty("targetLux", out var targetEl))
                            existingRoom.TargetLux = targetEl.GetInt32();

                        if (r.TryGetProperty("mode", out var modeEl))
                            existingRoom.Mode = modeEl.GetString();

                        if (r.TryGetProperty("isManualMode", out var isManual))
                            existingRoom.IsManualMode = isManual.GetBoolean();

                        // ✅ FIXÉ - Sensors avec TryGetProperty
                        if (r.TryGetProperty("sensors", out var sensorsElement))
                        {
                            existingRoom.Sensors.Clear();
                            foreach (var s in sensorsElement.EnumerateArray())
                            {
                                var sensor = new Sensor();

                                if (s.TryGetProperty("sensorId", out var sIdEl))
                                    sensor.SensorId = sIdEl.GetString();

                                if (s.TryGetProperty("type", out var typeEl))
                                    sensor.Type = typeEl.GetString();

                                if (s.TryGetProperty("value", out var valEl))
                                    sensor.Value = valEl.GetDouble();

                                if (s.TryGetProperty("lastUpdate", out var updateEl))
                                    sensor.LastUpdate = updateEl.GetInt64();

                                if (s.TryGetProperty("status", out var statusEl))
                                    sensor.Status = statusEl.GetString();

                                existingRoom.Sensors.Add(sensor);
                            }
                        }

                        // ✅ FIXÉ - Lamps avec TryGetProperty
                        if (r.TryGetProperty("lamps", out var lampsElement))
                        {
                            existingRoom.Lamps.Clear();
                            foreach (var l in lampsElement.EnumerateArray())
                            {
                                var lamp = new Lamp();

                                if (l.TryGetProperty("lampId", out var lIdEl))
                                    lamp.LampId = lIdEl.GetString();

                                if (l.TryGetProperty("power", out var powEl))
                                    lamp.Power = powEl.GetInt32();

                                if (l.TryGetProperty("pwm", out var pwmEl))
                                    lamp.Pwm = pwmEl.GetInt32();
                                else if (l.TryGetProperty("power", out var pow2El))
                                    lamp.Pwm = pow2El.GetInt32() * 255 / 100;

                                if (l.TryGetProperty("status", out var statEl))
                                    lamp.Status = statEl.GetString();

                                if (l.TryGetProperty("lastUpdate", out var updEl))
                                    lamp.LastUpdate = updEl.GetInt64();

                                existingRoom.Lamps.Add(lamp);
                            }
                        }
                    }
                    else
                    {
                        // ✅ FIXÉ - Nouvelle room avec TryGetProperty
                        var newRoom = new Room { RoomId = roomId };

                        if (r.TryGetProperty("lux", out var luxEl))
                            newRoom.Lux = luxEl.GetDouble();

                        if (r.TryGetProperty("lampPower", out var powerEl))
                            newRoom.LampPower = powerEl.GetInt32();

                        if (r.TryGetProperty("targetLux", out var targetEl))
                            newRoom.TargetLux = targetEl.GetInt32();

                        if (r.TryGetProperty("mode", out var modeEl))
                            newRoom.Mode = modeEl.GetString();

                        if (r.TryGetProperty("isManualMode", out var isManual))
                            newRoom.IsManualMode = isManual.GetBoolean();

                        // ✅ FIXÉ - Sensors
                        if (r.TryGetProperty("sensors", out var sensorsElement))
                        {
                            foreach (var s in sensorsElement.EnumerateArray())
                            {
                                var sensor = new Sensor();

                                if (s.TryGetProperty("sensorId", out var sIdEl))
                                    sensor.SensorId = sIdEl.GetString();

                                if (s.TryGetProperty("type", out var typeEl))
                                    sensor.Type = typeEl.GetString();

                                if (s.TryGetProperty("value", out var valEl))
                                    sensor.Value = valEl.GetDouble();

                                if (s.TryGetProperty("lastUpdate", out var updateEl))
                                    sensor.LastUpdate = updateEl.GetInt64();

                                if (s.TryGetProperty("status", out var statusEl))
                                    sensor.Status = statusEl.GetString();

                                newRoom.Sensors.Add(sensor);
                            }
                        }

                        // ✅ FIXÉ - Lamps
                        if (r.TryGetProperty("lamps", out var lampsElement))
                        {
                            foreach (var l in lampsElement.EnumerateArray())
                            {
                                var lamp = new Lamp();

                                if (l.TryGetProperty("lampId", out var lIdEl))
                                    lamp.LampId = lIdEl.GetString();

                                if (l.TryGetProperty("power", out var powEl))
                                    lamp.Power = powEl.GetInt32();

                                if (l.TryGetProperty("pwm", out var pwmEl))
                                    lamp.Pwm = pwmEl.GetInt32();
                                else if (l.TryGetProperty("power", out var pow2El))
                                    lamp.Pwm = pow2El.GetInt32() * 255 / 100;

                                if (l.TryGetProperty("status", out var statEl))
                                    lamp.Status = statEl.GetString();

                                if (l.TryGetProperty("lastUpdate", out var updEl))
                                    lamp.LastUpdate = updEl.GetInt64();

                                newRoom.Lamps.Add(lamp);
                            }
                        }

                        Rooms.Add(newRoom);
                        Debug.WriteLine($"✅ Room {newRoom.RoomId} ajoutée");
                    }
                }
            });
        }
        catch (KeyNotFoundException ex)
        {
            Debug.WriteLine($"❌ Clé manquante: {ex.Message}");
            Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur parsing JSON: {ex.Message}");
            Debug.WriteLine($"   Type: {ex.GetType().Name}");
        }
    }
}