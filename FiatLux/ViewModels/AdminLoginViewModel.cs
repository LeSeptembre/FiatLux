using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;
using FiatLux.Services;

namespace FiatLux.ViewModels;

public class AdminLoginViewModel : BindableObject
{
    private readonly WebSocketService _ws;
    private TaskCompletionSource<bool> _authTcs;

    public AdminLoginViewModel(WebSocketService ws)
    {
        _ws = ws;
        _ws.AdminAuthResponse += OnAdminAuthResponse;

        LoginCommand = new Command(async () => await Login(), () => !_isLoading);
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

    private bool _isLoading = false;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            ((Command)LoginCommand).ChangeCanExecute();
        }
    }

    public ICommand LoginCommand { get; }
    public ICommand BackCommand { get; }

    private void OnAdminAuthResponse(bool success, string message)
    {
        if (_authTcs != null && !_authTcs.Task.IsCompleted)
        {
            _authTcs.SetResult(success);
        }
    }

    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Entrez le mot de passe admin";
            return;
        }

        if (!_ws.IsConnected)
        {
            StatusMessage = "Non connecté au serveur";
            return;
        }

        IsLoading = true;
        StatusMessage = "Authentification...";

        try
        {
            _authTcs = new TaskCompletionSource<bool>();

            // ✅ Utilise RequestAdminAsync qui envoie automatiquement le sessionId
            await _ws.RequestAdminAsync(Password);

            // Attendre la réponse (timeout de 5 secondes)
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(_authTcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                StatusMessage = "Timeout - pas de réponse du serveur";
                IsLoading = false;
                return;
            }

            bool success = await _authTcs.Task;

            if (success)
            {
                StatusMessage = "✅ Authentification réussie";
                await Task.Delay(500);
                await Shell.Current.GoToAsync("//adminrooms");
            }
            else
            {
                StatusMessage = "❌ Mot de passe incorrect";
                Password = ""; // Vide le champ
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur: {ex.Message}";
            Debug.WriteLine($"❌ Erreur login admin: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
            _authTcs = null;
        }
    }
}