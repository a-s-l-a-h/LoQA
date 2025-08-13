using LoQA.Services;
// ADD: Using statement for MAUI Preferences
using Microsoft.Maui.Storage;

namespace LoQA.Views
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage(DatabaseService databaseService)
        {
            InitializeComponent();
        }

        // ADD: Load the saved setting when the page appears
        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Load the preference, defaulting to 'true' (on) if it's not set yet.
            //CopyOptionSwitch.IsToggled = Preferences.Default.Get("copy_feature_enabled", true);
        }

        // ADD: Save the setting whenever the switch is toggled
        /*private void CopyOptionSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            // Save the new value of the switch to persistent storage.
            Preferences.Default.Set("copy_feature_enabled", e.Value);
        }*/
    }
}