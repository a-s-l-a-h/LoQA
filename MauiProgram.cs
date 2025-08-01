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

        builder.Services.AddSingleton<IEasyChatWrapper, EasyChatEngine>();
        builder.Services.AddSingleton<EasyChatService>();
        builder.Services.AddSingleton<DatabaseService>();

        // Register HomePage as the main page singleton.
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddTransient<SettingsPage>();

        return builder.Build();
    }
}