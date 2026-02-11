using System.Text.Json;
using System.Windows.Input;
using FiatLux.Services;

namespace FiatLux.ViewModels;

public class RoomDetailsViewModel : BindableObject
{
    private readonly WebSocketService _ws;

    public string RoomId { get; }

    private string _currentMode = "Unknown";
    public string CurrentMode
    {
        get => _currentMode;
        set 
        { 
            _currentMode = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProSelected));
            OnPropertyChanged(nameof(IsConfortSelected));
            OnPropertyChanged(nameof(IsAmbianceSelected));
            OnPropertyChanged(nameof(IsTamiseSelected));
        }
    }

    private double _currentLux = 0;
    public double CurrentLux
    {
        get => _currentLux;
        set { _currentLux = value; OnPropertyChanged(); }
    }

    private int _currentPower = 0;
    public int CurrentPower
    {
        get => _currentPower;
        set { _currentPower = value; OnPropertyChanged(); }
    }

    private int _targetLux = 0;
    public int TargetLux
    {
        get => _targetLux;
        set { _targetLux = value; OnPropertyChanged(); }
    }

    private bool _isManualMode = false;
    public bool IsManualMode
    {
        get => _isManualMode;
        set 
        { 
            _isManualMode = value; 
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanChangeMode));
        }
    }

    // True si l'utilisateur PEUT changer le mode (pas verrouillé)
    public bool CanChangeMode => !IsManualMode;

    // Properties to check which mode is selected
    public bool IsProSelected => CurrentMode == "Professional" || CurrentMode == "Professionnel";
    public bool IsConfortSelected => CurrentMode == "Comfort" || CurrentMode == "Confort";
    public bool IsAmbianceSelected => CurrentMode == "Ambiance";
    public bool IsTamiseSelected => CurrentMode == "Dimmed" || CurrentMode == "Tamisé";

    public ICommand ProCommand { get; }
    public ICommand ConfortCommand { get; }
    public ICommand AmbianceCommand { get; }
    public ICommand TamiseCommand { get; }
    public ICommand BackCommand { get; }

    public RoomDetailsViewModel(WebSocketService ws, string roomId)
    {
        _ws = ws;
        RoomId = roomId;

        ProCommand = new Command(() => SendMode("Professional", 750));
        ConfortCommand = new Command(() => SendMode("Comfort", 500));
        AmbianceCommand = new Command(() => SendMode("Ambiance", 300));
        TamiseCommand = new Command(() => SendMode("Dimmed", 150));
        BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));

        // Subscribe to WebSocket messages to update real-time data
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
                        CurrentLux = r.GetProperty("lux").GetDouble();
                        CurrentPower = r.GetProperty("lampPower").GetInt32();
                        TargetLux = r.GetProperty("targetLux").GetInt32();
                        CurrentMode = r.GetProperty("mode").GetString();
                        
                        // Récupère le statut de verrouillage
                        if (r.TryGetProperty("isManualMode", out var isManual))
                        {
                            IsManualMode = isManual.GetBoolean();
                        }
                    });
                    break;
                }
            }
        }
        catch { }
    }

    private async void SendMode(string mode, int target)
    {
        if (!_ws.IsConnected)
            return;

        // Bloque si la salle est en mode manuel (verrouillée par admin)
        if (IsManualMode)
        {
            await Application.Current.MainPage.DisplayAlert(
                "Salle verrouillée", 
                "Cette salle est en mode manuel. Contactez un administrateur.", 
                "OK");
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            command = "setMode",
            roomId = RoomId,
            mode = mode,
            target = target
        });

        await _ws.SendAsync(payload);
        CurrentMode = mode;
        TargetLux = target;
    }
}
