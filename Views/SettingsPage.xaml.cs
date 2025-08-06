using LoQA.Services;

namespace LoQA.Views
{
    public partial class SettingsPage : ContentPage
    {
        private readonly EasyChatService _chatService;

        public SettingsPage(DatabaseService databaseService, EasyChatService chatService)
        {
            InitializeComponent();
            _chatService = chatService;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // FIX: Use the full namespace to resolve ambiguity
            TemplateEditor.Text = Microsoft.Maui.Storage.Preferences.Get("FallbackTemplate", string.Empty);
            TemplateStatusLabel.Text = string.Empty;
        }

        private async void BackButton_Clicked(object? sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        private void SaveTemplate_Clicked(object sender, EventArgs e)
        {
            var template = TemplateEditor.Text;
            bool success = _chatService.SetFallbackTemplate(template);

            if (success)
            {
                TemplateStatusLabel.Text = "Template saved successfully!";
            }
            else
            {
                TemplateStatusLabel.Text = "Failed to save template.";
            }
        }
    }
}