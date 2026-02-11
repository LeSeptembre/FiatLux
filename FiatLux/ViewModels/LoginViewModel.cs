using System.Diagnostics;
using System.Windows.Input;
using FiatLux.Services;

namespace FiatLux.ViewModels;

public class LoginViewModel : BindableObject
{
    private readonly WebSocketService _ws;

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

    private string _status = "";
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
            Status = "Connecting...";
            await _ws.ConnectAsync(Server);
            Status = "Connected";
            Debug.WriteLine("✅ Connected to WebSocket server");

            await Shell.Current.GoToAsync("//rooms");
        }
        catch (Exception ex)
        {
            Status = "Connection error";
            Debug.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}
