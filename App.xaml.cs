using LoQA.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace LoQA
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());

            window.Destroying += (s, e) =>
            {
                if (IPlatformApplication.Current?.Services == null) return;

                var chatService = IPlatformApplication.Current.Services.GetService<EasyChatService>();

                if (chatService != null && chatService.CurrentEngineState != EngineState.UNINITIALIZED)
                {
                    Debug.WriteLine("App is closing. Disposing EasyChatService to unload model and free native resources...");
                    chatService.Dispose();
                    Debug.WriteLine("Chat service disposed and model successfully unloaded.");
                }
            };

            return window;
        }
    }
}