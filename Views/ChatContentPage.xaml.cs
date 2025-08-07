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
            BindingContext = _chatService;
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
            switch (e.PropertyName)
            {
                case nameof(EasyChatService.CurrentEngineState):
                case nameof(EasyChatService.CurrentConversation):
                case nameof(EasyChatService.LoadedModel):
                case nameof(EasyChatService.LastErrorMessage):
                case nameof(EasyChatService.IsGenerating):
                case nameof(EasyChatService.IsHistoryLoadPending):
                    MainThread.BeginInvokeOnMainThread(UpdateStatusLabel);
                    break;
            }
        }

        private void UpdateStatusLabel()
        {
            if (_chatService.IsGenerating)
            {
                StatusLabel.Text = "Generating...";
            }
            else if (_chatService.IsHistoryLoadPending)
            {
                StatusLabel.Text = "Previewing history. Click below to load.";
            }
            else if (_chatService.CurrentEngineState == EngineState.IDLE)
            {
                var modelName = _chatService.LoadedModel?.Name ?? "Active";
                var chatName = _chatService.CurrentConversation?.Name ?? "New Chat";
                StatusLabel.Text = $"{chatName} ({modelName})";
            }
            else if (_chatService.CurrentEngineState == EngineState.UNINITIALIZED)
            {
                StatusLabel.Text = "No model loaded. Go to Models page.";
            }
            else if (_chatService.CurrentEngineState == EngineState.IN_ERROR)
            {
                StatusLabel.Text = $"Error: {_chatService.LastErrorMessage}";
            }
            else
            {
                StatusLabel.Text = $"State: {_chatService.CurrentEngineState}";
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
            _chatService.AbortCurrentTask();
        }

        private async void LoadHistoryButton_Clicked(object sender, EventArgs e)
        {
            await _chatService.LoadPendingHistoryIntoEngineAsync();
        }
    }
}