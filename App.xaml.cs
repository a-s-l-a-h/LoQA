// Add these using statements at the top
using LoQA.Services;
using Microsoft.Extensions.DependencyInjection;

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
            // === ADDED LIFECYCLE HOOK FOR PROPER SHUTDOWN CLEANUP                  ===
            // =========================================================================
            // The 'Destroying' event is triggered when the app window is about to close.
            window.Destroying += (s, e) =>
            {
                // We need to get the instance of our service from the Dependency Injection container.
                var chatService = IPlatformApplication.Current?.Services.GetService<EasyChatService>();

                // If the service exists and has a model loaded...
                if (chatService != null && chatService.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("App is closing. Unloading model to free resources...");

                    // ...call our new Dispose method. This will chain the call down to the
                    // native freeLlama() function in the C++ library.
                    chatService.Dispose();

                    System.Diagnostics.Debug.WriteLine("Model successfully unloaded.");
                }
            };
            // --- END OF ADDED CODE ---

            return window;
        }
    }
}