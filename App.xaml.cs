// Add these using statements at the top
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

            // =========================================================================
            // === ACTION: ADDED LIFECYCLE HOOK FOR PROPER SHUTDOWN CLEANUP          ===
            // =========================================================================
            // The 'Destroying' event is triggered when the app window is about to close.
            window.Destroying += (s, e) =>
            {
                // Resolve the singleton instance of our service from the Dependency Injection container.
                var chatService = IPlatformApplication.Current?.Services.GetService<EasyChatService>();

                // If the service exists and has a model loaded...
                if (chatService != null && chatService.IsInitialized)
                {
                    Debug.WriteLine("App is closing. Disposing EasyChatService to unload model and free native resources...");

                    // ...call our Dispose method. This chains the call down to the
                    // native freeLlama() function via EasyChatEngine.Dispose().
                    // This is critical for releasing potentially gigabytes of memory.
                    chatService.Dispose();

                    Debug.WriteLine("Chat service disposed and model successfully unloaded.");
                }
            };
            // --- END OF ADDED CODE ---

            return window;
        }
    }
}