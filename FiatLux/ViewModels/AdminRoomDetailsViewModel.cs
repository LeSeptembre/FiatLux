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
                if (!r.TryGetProperty("roomId", out var roomIdEl))
                    continue;

                var id = roomIdEl.GetString();
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
            if (!sensorElement.TryGetProperty("sensorId", out var sensorIdElement))
                continue;

            var sensorId = sensorIdElement.GetString();
            var existing = Sensors.FirstOrDefault(s => s.SensorId == sensorId);

            if (existing != null)
            {
                // Met à jour le capteur existant
                if (sensorElement.TryGetProperty("value", out var valueEl))
                    existing.Value = valueEl.GetDouble();

                if (sensorElement.TryGetProperty("lastUpdate", out var updateEl))
                    existing.LastUpdate = updateEl.GetInt64();

                if (sensorElement.TryGetProperty("status", out var statusEl))
                    existing.Status = statusEl.GetString();
            }
            else
            {
                // Ajoute le nouveau capteur
                var newSensor = new Sensor { SensorId = sensorId, Type = "lux" };

                if (sensorElement.TryGetProperty("value", out var valueEl))
                    newSensor.Value = valueEl.GetDouble();

                if (sensorElement.TryGetProperty("lastUpdate", out var updateEl))
                    newSensor.LastUpdate = updateEl.GetInt64();

                if (sensorElement.TryGetProperty("status", out var statusEl))
                    newSensor.Status = statusEl.GetString();

                Sensors.Add(newSensor);
            }
        }
    }

    private void UpdateLamps(JsonElement room)
    {
        if (!room.TryGetProperty("lamps", out var lampsElement))
            return;

        foreach (var l in lampsElement.EnumerateArray())
        {
            if (!l.TryGetProperty("lampId", out var lampIdElement))
                continue;

            var lampId = lampIdElement.GetString();
            var existing = Lamps.FirstOrDefault(lamp => lamp.LampId == lampId);

            if (existing != null)
            {
                // Met à jour la lampe existante
                if (l.TryGetProperty("power", out var powerEl))
                    existing.Power = powerEl.GetInt32();

                if (l.TryGetProperty("pwm", out var pwmEl))
                    existing.Pwm = pwmEl.GetInt32();
                else if (l.TryGetProperty("power", out var powerEl2))
                    existing.Pwm = powerEl2.GetInt32() * 255 / 100;

                if (l.TryGetProperty("status", out var statusEl))
                    existing.Status = statusEl.GetString();

                if (l.TryGetProperty("lastUpdate", out var updateEl))
                    existing.LastUpdate = updateEl.GetInt64();
            }
            else
            {
                // Ajoute la nouvelle lampe
                var newLamp = new Lamp { LampId = lampId };

                if (l.TryGetProperty("power", out var powerEl))
                    newLamp.Power = powerEl.GetInt32();

                if (l.TryGetProperty("pwm", out var pwmEl))
                    newLamp.Pwm = pwmEl.GetInt32();
                else
                    newLamp.Pwm = newLamp.Power * 255 / 100;

                if (l.TryGetProperty("status", out var statusEl))
                    newLamp.Status = statusEl.GetString();

                if (l.TryGetProperty("lastUpdate", out var updateEl))
                    newLamp.LastUpdate = updateEl.GetInt64();

                Lamps.Add(newLamp);
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