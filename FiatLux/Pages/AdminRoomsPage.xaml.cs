using FiatLux.Models;
using FiatLux.ViewModels;

namespace FiatLux.Pages;

public partial class AdminRoomsPage : ContentPage
{
    public AdminRoomsPage(RoomsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    private async void OnRoomSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Room room)
        {
            await Shell.Current.GoToAsync("adminroomdetails",
                new Dictionary<string, object>
                {
                    { "roomId", room.RoomId }
                });

            ((CollectionView)sender).SelectedItem = null;
        }
    }
}
