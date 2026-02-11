using FiatLux.Services;
using FiatLux.ViewModels;

namespace FiatLux.Pages;

[QueryProperty(nameof(RoomId), "roomId")]
public partial class RoomDetailsPage : ContentPage
{
    private readonly WebSocketService _ws;

    public string RoomId { get; set; }

    public RoomDetailsPage(WebSocketService ws)
    {
        InitializeComponent();
        _ws = ws;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext == null && !string.IsNullOrEmpty(RoomId))
        {
            BindingContext = new RoomDetailsViewModel(_ws, RoomId);
        }
    }
}
