// MauiProgram.cs

using LoQA.Services;
using LoQA.Views;
using Microsoft.Extensions.Logging;

namespace LoQA;

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
#endif

        // Register your services here
        builder.Services.AddSingleton<EasyChatService>();
        builder.Services.AddSingleton<DatabaseService>();

        // Register your pages here
        builder.Services.AddSingleton<ChatPage>(); // Singleton for persistence
        builder.Services.AddTransient<SettingsPage>(); // Transient is fine here

        return builder.Build();
    }
}