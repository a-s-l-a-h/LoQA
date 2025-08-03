using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class ChatContentPage : ContentPage
    {
        private EasyChatService? _chatService;
        private bool _isUserScrolledUp = false;

        public ChatContentPage()
        {
            InitializeComponent();
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            if (BindingContext is EasyChatService service)
            {
                if (_chatService != null)
                {
                    _chatService.PropertyChanged -= OnServicePropertyChanged;
                }

                _chatService = service;
                _chatService.PropertyChanged += OnServicePropertyChanged;

                if (_chatService.IsInitialized) SetReadyState();
                UpdateGeneratingState(_chatService.IsGenerating);
                UpdateStatusLabel();
            }
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_chatService == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.IsGenerating):
                        UpdateGeneratingState(_chatService.IsGenerating);
                        break;
                    case nameof(EasyChatService.CurrentConversation):
                    case nameof(EasyChatService.LoadedModel):
                        UpdateStatusLabel();
                        break;
                    case nameof(EasyChatService.IsInitialized):
                        if (_chatService.IsInitialized) SetReadyState();
                        else SetUnreadyState();
                        break;
                }
            });
        }

        private void UpdateGeneratingState(bool isGenerating)
        {
            SendButton.IsVisible = !isGenerating;
            StopButton.IsVisible = isGenerating;
            PromptEditor.IsEnabled = !isGenerating;

            UpdateStatusLabel();
            if (!isGenerating && _chatService != null && _chatService.IsInitialized)
            {
                PromptEditor.Focus();
            }
        }

        private void UpdateStatusLabel()
        {
            if (_chatService == null) return;
            if (_chatService.IsGenerating)
            {
                StatusLabel.Text = "Generating...";
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
            if (_chatService == null || string.IsNullOrWhiteSpace(PromptEditor.Text)) return;
            string prompt = PromptEditor.Text.Trim();
            PromptEditor.Text = string.Empty;
            await _chatService.SendMessageAsync(prompt);
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            _chatService?.StopGeneration();
        }

        private void SetUnreadyState()
        {
            StatusLabel.Text = "No model loaded. Go to Models page.";
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
            InputGrid.IsEnabled = false;
        }

        private void SetReadyState()
        {
            InputGrid.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;
            PromptEditor.Focus();
            UpdateStatusLabel();
        }

        private void ChatMessagesView_Scrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            if (_chatService == null || _chatService.CurrentMessages.Count == 0) return;
            if (e.LastVisibleItemIndex < _chatService.CurrentMessages.Count - 2)
            {
                _isUserScrolledUp = true;
            }
            else
            {
                _isUserScrolledUp = false;
            }
        }
    }
}