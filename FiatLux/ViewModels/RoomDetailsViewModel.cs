using System.Text.Json;
using System.Windows.Input;
using System.Diagnostics;
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

    public bool CanChangeMode => !IsManualMode;

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

        _ws.MessageReceived += OnMessageReceived;
    }

    private void OnMessageReceived(string json)
    {
        try
        {
            Debug.WriteLine("📦 Message reçu dans ViewModel");

            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("rooms", out var roomsElement))
            {
                Debug.WriteLine("⚠️ Pas de clé 'rooms' dans le JSON");
                return;
            }

            foreach (var r in roomsElement.EnumerateArray())
            {
                // ✅ SÉCURISÉ - TryGetProperty au lieu de GetProperty
                if (!r.TryGetProperty("roomId", out var roomIdEl))
                {
                    Debug.WriteLine("⚠️ Pas de clé 'roomId' dans une room");
                    continue;
                }

                var id = roomIdEl.GetString();
                if (id == RoomId)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        try
                        {
                            // ✅ SÉCURISÉ - Vérifie chaque propriété avant de l'utiliser
                            if (r.TryGetProperty("lux", out var luxEl))
                            {
                                CurrentLux = luxEl.GetDouble();
                                Debug.WriteLine($"  ✓ lux = {CurrentLux}");
                            }
                            else
                            {
                                Debug.WriteLine("  ⚠️ Clé 'lux' absente");
                            }

                            if (r.TryGetProperty("lampPower", out var powerEl))
                            {
                                CurrentPower = powerEl.GetInt32();
                                Debug.WriteLine($"  ✓ lampPower = {CurrentPower}");
                            }
                            else
                            {
                                Debug.WriteLine("  ⚠️ Clé 'lampPower' absente");
                            }

                            if (r.TryGetProperty("targetLux", out var targetEl))
                            {
                                TargetLux = targetEl.GetInt32();
                                Debug.WriteLine($"  ✓ targetLux = {TargetLux}");
                            }
                            else
                            {
                                Debug.WriteLine("  ⚠️ Clé 'targetLux' absente");
                            }

                            if (r.TryGetProperty("mode", out var modeEl))
                            {
                                CurrentMode = modeEl.GetString();
                                Debug.WriteLine($"  ✓ mode = {CurrentMode}");
                            }
                            else
                            {
                                Debug.WriteLine("  ⚠️ Clé 'mode' absente");
                            }

                            if (r.TryGetProperty("isManualMode", out var isManual))
                            {
                                IsManualMode = isManual.GetBoolean();
                                Debug.WriteLine($"  ✓ isManualMode = {IsManualMode}");
                            }
                            else
                            {
                                Debug.WriteLine("  ⚠️ Clé 'isManualMode' absente");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ Erreur dans MainThread: {ex.Message}");
                            Debug.WriteLine($"   Type: {ex.GetType().Name}");
                            Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
                        }
                    });
                    break;
                }
            }
        }
        catch (KeyNotFoundException ex)
        {
            Debug.WriteLine($"❌ Clé manquante dans JSON!");
            Debug.WriteLine($"   Message: {ex.Message}");
            Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
            Debug.WriteLine($"   JSON reçu: {json}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Erreur parsing JSON: {ex.Message}");
            Debug.WriteLine($"   Type: {ex.GetType().Name}");
            Debug.WriteLine($"   StackTrace: {ex.StackTrace}");
        }
    }

    private async void SendMode(string mode, int target)
    {
        if (!_ws.IsConnected)
            return;

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