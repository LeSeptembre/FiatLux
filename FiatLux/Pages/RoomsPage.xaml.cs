using FiatLux.Models;
using FiatLux.ViewModels;

namespace FiatLux.Pages;

public partial class RoomsPage : ContentPage
{
    public RoomsPage(RoomsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Room room)
        {
            await Shell.Current.GoToAsync("roomdetails",
                new Dictionary<string, object>
                {
                    { "roomId", room.RoomId }
                });

            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
