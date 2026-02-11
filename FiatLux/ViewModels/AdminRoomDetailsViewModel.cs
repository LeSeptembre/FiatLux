using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows.Input;
using FiatLux.Models;
using FiatLux.Services;

namespace FiatLux.ViewModels;

[QueryProperty(nameof(RoomId), "roomId")]
public class AdminRoomDetailsViewModel : BindableObject
{
    private readonly WebSocketService _ws;

    public string RoomId { get; set; }

    public ObservableCollection<Sensor> Sensors { get; } = new();
    public ObservableCollection<Lamp> Lamps { get; } = new();

    private bool _isManualMode = false;
    private bool _updatingFromServer = false;

    public bool IsManualMode
    {
        get => _isManualMode;
        set
        {
            if (_isManualMode != value && !_updatingFromServer)
            {
                _isManualMode = value;
                OnPropertyChanged();
                ToggleManualMode();
            }
        }
    }

    private int _manualTarget = 500;
    public int ManualTarget
    {
        get => _manualTarget;
        set
        {
            _manualTarget = value;
            OnPropertyChanged();
        }
    }

    public ICommand ApplyManualTargetCommand { get; }
    public ICommand BackCommand { get; }

    public AdminRoomDetailsViewModel(WebSocketService ws, string roomId)
    {
        _ws = ws;
        RoomId = roomId;

        ApplyManualTargetCommand = new Command(async () => await ApplyManualTarget());
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));

        _ws.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rooms", out var roomsElement))
                return;

            foreach (var r in roomsElement.EnumerateArray())
            {
                var id = r.GetProperty("roomId").GetString();
                if (id != RoomId)
                    continue;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UpdateManualModeStatus(r);
                    UpdateSensors(r);  // ✅ CHANGÉ: sensors au pluriel
                    UpdateLamps(r);
                });

                break;
            }
        }
        catch { }
    }

    private void UpdateManualModeStatus(JsonElement room)
    {
        if (!room.TryGetProperty("isManualMode", out var isManual))
            return;

        _updatingFromServer = true;
        _isManualMode = isManual.GetBoolean();
        OnPropertyChanged(nameof(IsManualMode));
        _updatingFromServer = false;
    }

    private void UpdateSensors(JsonElement room)
    {
        // ✅ FIXÉ: Bridge v3 envoie "sensors" (tableau), pas "sensor" (objet)
        if (!room.TryGetProperty("sensors", out var sensorsElement))
            return;

        // ✅ FIXÉ: Boucle sur le tableau de capteurs
        foreach (var sensorElement in sensorsElement.EnumerateArray())
        {
            var sensorId = sensorElement.GetProperty("sensorId").GetString();
            var existing = Sensors.FirstOrDefault(s => s.SensorId == sensorId);

            if (existing != null)
            {
                // Met à jour le capteur existant
                existing.Value = sensorElement.GetProperty("value").GetDouble();
                existing.LastUpdate = sensorElement.GetProperty("lastUpdate").GetInt64();
                existing.Status = sensorElement.GetProperty("status").GetString();
            }
            else
            {
                // Ajoute le nouveau capteur
                Sensors.Add(new Sensor
                {
                    SensorId = sensorId,
                    Type = "lux",
                    Value = sensorElement.GetProperty("value").GetDouble(),
                    LastUpdate = sensorElement.GetProperty("lastUpdate").GetInt64(),
                    Status = sensorElement.GetProperty("status").GetString()
                });
            }
        }
    }

    private void UpdateLamps(JsonElement room)
    {
        if (!room.TryGetProperty("lamps", out var lampsElement))
            return;

        foreach (var l in lampsElement.EnumerateArray())
        {
            var lampId = l.GetProperty("lampId").GetString();
            var existing = Lamps.FirstOrDefault(lamp => lamp.LampId == lampId);

            if (existing != null)
            {
                // Met à jour la lampe existante
                existing.Power = l.GetProperty("power").GetInt32();
                existing.Pwm = l.TryGetProperty("pwm", out var pwm) ? pwm.GetInt32() : existing.Power * 255 / 100;
                existing.Status = l.GetProperty("status").GetString();
                existing.LastUpdate = l.GetProperty("lastUpdate").GetInt64();
            }
            else
            {
                // Ajoute la nouvelle lampe
                Lamps.Add(new Lamp
                {
                    LampId = lampId,
                    Power = l.GetProperty("power").GetInt32(),
                    Pwm = l.TryGetProperty("pwm", out var pwm) ? pwm.GetInt32() : l.GetProperty("power").GetInt32() * 255 / 100,
                    Status = l.GetProperty("status").GetString(),
                    LastUpdate = l.GetProperty("lastUpdate").GetInt64()
                });
            }
        }
    }

    private async void ToggleManualMode()
    {
        if (!_ws.IsConnected)
            return;

        if (!_ws.IsAdmin)
        {
            await Application.Current.MainPage.DisplayAlert("Erreur", "Token admin non valide", "OK");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            command = "setManualMode",
            roomId = RoomId,
            enabled = IsManualMode,
            adminToken = _ws.AdminToken,
            sessionId = _ws.SessionId
        });

        await _ws.SendAsync(payload);
    }

    private async Task ApplyManualTarget()
    {
        if (!_ws.IsConnected || !IsManualMode)
            return;

        if (!_ws.IsAdmin)
        {
            await Application.Current.MainPage.DisplayAlert("Erreur", "Token admin non valide", "OK");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            command = "setManualTarget",
            roomId = RoomId,
            target = ManualTarget,
            adminToken = _ws.AdminToken,
            sessionId = _ws.SessionId
        });

        await _ws.SendAsync(payload);
    }
}