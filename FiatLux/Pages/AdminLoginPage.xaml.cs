using FiatLux.ViewModels;

namespace FiatLux.Pages;

public partial class AdminLoginPage : ContentPage
{
    public AdminLoginPage(AdminLoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
