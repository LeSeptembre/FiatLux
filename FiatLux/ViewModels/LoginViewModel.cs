using System.Diagnostics;
using System.Windows.Input;
using FiatLux.Services;

namespace FiatLux.ViewModels;

public class LoginViewModel : BindableObject
{
    private readonly WebSocketService _ws;

    // ✅ Injection du WebSocket partagé
    public LoginViewModel(WebSocketService ws)
    {
        _ws = ws;
        ConnectCommand = new Command(async () => await Connect());
    }

    private string _server = "ws://10.0.0.109:81";
    public string Server
    {
        get => _server;
        set { _server = value; OnPropertyChanged(); }
    }

    private string _status = "Déconnecté";
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public ICommand ConnectCommand { get; }

    private async Task Connect()
    {
        try
        {
            Status = "Connexion en cours...";
            await _ws.ConnectAsync(Server);
            Status = "Connecté ✅";
            Debug.WriteLine("✅ Connecté au serveur WebSocket");

            await Shell.Current.GoToAsync("//rooms");
        }
        catch (Exception ex)
        {
            Status = "Erreur de connexion ❌";
            Debug.WriteLine($"❌ Erreur: {ex.Message}");
        }
    }
}