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

        builder.Services.AddSingleton<EasyChatEngine>();
        builder.Services.AddSingleton<EasyChatService>();
        builder.Services.AddSingleton<DatabaseService>();

        builder.Services.AddSingleton<ChatContentPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<ModelsPage>();

        return builder.Build();
    }
}