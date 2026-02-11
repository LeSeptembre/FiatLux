using FiatLux.Services;
using FiatLux.ViewModels;

namespace FiatLux.Pages;

[QueryProperty(nameof(RoomId), "roomId")]
public partial class AdminRoomDetailsPage : ContentPage
{
    private readonly WebSocketService _ws;
    public string RoomId { get; set; }

    public AdminRoomDetailsPage(WebSocketService ws)
    {
        InitializeComponent();
        _ws = ws;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext == null && !string.IsNullOrEmpty(RoomId))
        {
            BindingContext = new AdminRoomDetailsViewModel(_ws, RoomId);
        }
    }
}
