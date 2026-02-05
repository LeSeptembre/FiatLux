using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using FiatLux.Services;

namespace FiatLux.ViewModels;

public class AdminLoginViewModel : BindableObject
{
    private readonly WebSocketService _ws;

    public AdminLoginViewModel(WebSocketService ws)
    {
        _ws = ws;
        LoginCommand = new Command(async () => await Login());
        BackCommand = new Command(async () => await Shell.Current.GoToAsync("//rooms"));
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); }
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand LoginCommand { get; }
    public ICommand BackCommand { get; }

    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Please enter a password";
            return;
        }

        if (!_ws.IsConnected)
        {
            StatusMessage = "Not connected to server";
            return;
        }

        StatusMessage = "Authenticating...";

        var payload = JsonSerializer.Serialize(new
        {
            command = "requestAdmin",
            password = Password
        });

        await _ws.SendAsync(payload);

        // Attendre la réponse (sera gérée par un event dans WebSocketService)
        // Pour l'instant on redirige directement
        await Task.Delay(500);
        
        StatusMessage = "Login successful";
        await Task.Delay(500);
        
        await Shell.Current.GoToAsync("//adminrooms");
    }
}
