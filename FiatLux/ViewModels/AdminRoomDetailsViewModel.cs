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
    public bool IsManualMode
    {
        get => _isManualMode;
        set
        {
            if (_isManualMode != value)
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
        set { _manualTarget = value; OnPropertyChanged(); }
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
                if (id == RoomId)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        // Update manual mode status
                        if (r.TryGetProperty("isManualMode", out var isManual))
                        {
                            _isManualMode = isManual.GetBoolean();
                            OnPropertyChanged(nameof(IsManualMode));
                        }

                        // Update sensors
                        Sensors.Clear();
                        if (r.TryGetProperty("sensors", out var sensorsElement))
                        {
                            foreach (var s in sensorsElement.EnumerateArray())
                            {
                                Sensors.Add(new Sensor
                                {
                                    SensorId = s.GetProperty("sensorId").GetString(),
                                    Type = s.GetProperty("type").GetString(),
                                    Value = s.GetProperty("value").GetDouble(),
                                    LastUpdate = s.GetProperty("lastUpdate").GetInt64(),
                                    Status = s.GetProperty("status").GetString()
                                });
                            }
                        }

                        // Update lamps
                        Lamps.Clear();
                        if (r.TryGetProperty("lamps", out var lampsElement))
                        {
                            foreach (var l in lampsElement.EnumerateArray())
                            {
                                Lamps.Add(new Lamp
                                {
                                    LampId = l.GetProperty("lampId").GetString(),
                                    Power = l.GetProperty("power").GetInt32(),
                                    Status = l.GetProperty("status").GetString(),
                                    LastUpdate = l.GetProperty("lastUpdate").GetInt64()
                                });
                            }
                        }
                    });
                    break;
                }
            }
        }
        catch { }
    }

    private async void ToggleManualMode()
    {
        if (!_ws.IsConnected)
            return;

        var payload = JsonSerializer.Serialize(new
        {
            command = "setManualMode",
            roomId = RoomId,
            enabled = IsManualMode
        });

        await _ws.SendAsync(payload);
    }

    private async Task ApplyManualTarget()
    {
        if (!_ws.IsConnected || !IsManualMode)
            return;

        var payload = JsonSerializer.Serialize(new
        {
            command = "setManualTarget",
            roomId = RoomId,
            target = ManualTarget
        });

        await _ws.SendAsync(payload);
    }
}
