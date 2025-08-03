namespace LoQA
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register the route for the settings page
            Routing.RegisterRoute(nameof(Views.SettingsPage), typeof(Views.SettingsPage));
            // Register the route for the models page
            Routing.RegisterRoute(nameof(Views.ModelsPage), typeof(Views.ModelsPage));
        }
    }
}