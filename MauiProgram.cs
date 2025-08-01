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

        // --- UPDATED SERVICE REGISTRATION ---

        // Register the low-level engine as the implementation for the wrapper interface.
        // It's a singleton because you only want one instance of the native model in memory.
        builder.Services.AddSingleton<IEasyChatWrapper, EasyChatEngine>();

        // Register the new high-level service that manages conversation logic.
        // It's a singleton to maintain state across the application.
        builder.Services.AddSingleton<EasyChatService>();

        // Register the database service.
        builder.Services.AddSingleton<DatabaseService>();

        // Register pages. ChatPage remains a singleton to preserve its state.
        builder.Services.AddSingleton<ChatPage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}