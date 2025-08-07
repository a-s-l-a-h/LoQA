using LoQA.Services;

namespace LoQA.Views
{
    public partial class SettingsPage : ContentPage
    {
        // No longer needs EasyChatService for template setting.
        public SettingsPage(DatabaseService databaseService)
        {
            InitializeComponent();
        }

        // All event handlers related to saving templates have been removed.
    }
}