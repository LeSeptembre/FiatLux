using FiatLux.ViewModels;

namespace FiatLux.Pages;

public partial class LoginPage : ContentPage
{
    // ✅ Injection du ViewModel
    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}