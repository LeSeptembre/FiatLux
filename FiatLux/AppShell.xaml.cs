using FiatLux.Pages;

namespace FiatLux;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        Routing.RegisterRoute("roomdetails", typeof(RoomDetailsPage));
        Routing.RegisterRoute("adminroomdetails", typeof(AdminRoomDetailsPage));
    }
}
