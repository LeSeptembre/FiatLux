using FiatLux.Pages;
using FiatLux.Services;
using FiatLux.ViewModels;
using Microsoft.Extensions.Logging;

namespace FiatLux
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
            builder.Services.AddSingleton<WebSocketService>();

            builder.Services.AddSingleton<RoomsViewModel>();
            builder.Services.AddSingleton<LoginViewModel>();

            builder.Services.AddSingleton<RoomsPage>();
            builder.Services.AddSingleton<LoginPage>();
            builder.Services.AddSingleton<RoomDetailsPage>();

#endif

            return builder.Build();
        }
    }
}
