using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class ChatContentPage : ContentPage
    {
        private readonly EasyChatService _chatService;

        public ChatContentPage(EasyChatService chatService)
        {
            InitializeComponent();
            _chatService = chatService;
            BindingContext = _chatService; // This is key for all bindings to work
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _chatService.PropertyChanged += OnServicePropertyChanged;
            UpdateStatusLabel();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.PropertyChanged -= OnServicePropertyChanged;
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_chatService == null) return;

            // Only properties that require manual UI updates should be here.
            // Most things are handled by binding now.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.CurrentConversation):
                    case nameof(EasyChatService.LoadedModel):
                    case nameof(EasyChatService.IsGenerating):
                    case nameof(EasyChatService.IsInitialized):
                        UpdateStatusLabel();
                        break;
                }
            });
        }

        private void UpdateStatusLabel()
        {
            if (_chatService == null) return;

            if (_chatService.IsGenerating)
            {
                StatusLabel.Text = "Generating...";
            }
            else if (_chatService.IsHistoryLoadPending)
            {
                StatusLabel.Text = "Previewing history. Click below to load.";
            }
            else if (_chatService.IsInitialized)
            {
                var modelName = _chatService.LoadedModel?.Name ?? "Active";
                var chatName = _chatService.CurrentConversation?.Name ?? "New Chat";
                StatusLabel.Text = $"{chatName} ({modelName})";
            }
            else
            {
                StatusLabel.Text = "No model loaded. Go to Models page.";
            }
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PromptEditor.Text)) return;
            string prompt = PromptEditor.Text.Trim();
            PromptEditor.Text = string.Empty;
            await _chatService.SendMessageAsync(prompt);
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            _chatService.StopGeneration();
        }

        // Event handler for the new button
        private async void LoadHistoryButton_Clicked(object sender, EventArgs e)
        {
            await _chatService.LoadPendingHistoryIntoEngineAsync();
        }
    }
}