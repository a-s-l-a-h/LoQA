// MauiProgram.cs

using LoQA.Services;
using LoQA.Views;
using Microsoft.Extensions.Logging;

namespace LoQA; // Make sure your namespace matches your project

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>() // FIX: Use the 'App' class from App.xaml.cs
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

        // Register the page
        builder.Services.AddTransient<ChatPage>();

        return builder.Build();
    }
}