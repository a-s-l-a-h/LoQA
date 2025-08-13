// C:\MYWORLD\Projects\LoQA\LoQA\Views\ChatContentPage.xaml.cs
using LoQA.Services;
using System.ComponentModel;
// ADD: These using statements are now needed
using LoQA.Models;
using Microsoft.Maui.Storage;
using System.Runtime.CompilerServices;

namespace LoQA.Views
{
    // MODIFY: Implement INotifyPropertyChanged
    public partial class ChatContentPage : ContentPage, INotifyPropertyChanged
    {
        private readonly EasyChatService _chatService;

        // ADD: A bindable property to control visibility
        private bool _isCopyVisible;
        public bool IsCopyVisible
        {
            get => _isCopyVisible;
            set
            {
                _isCopyVisible = value;
                OnPropertyChanged(); // Notify the UI that this value has changed
            }
        }

        public ChatContentPage(EasyChatService chatService)
        {
            InitializeComponent();
            _chatService = chatService;

            // MODIFY: Set the BindingContext of the page to both the service AND itself
            // The main context is the service, but setting it to 'this' allows bindings to page properties.
            BindingContext = _chatService;
        }

        // MODIFY: OnAppearing now also checks the setting
        protected override void OnAppearing()
        {
            base.OnAppearing();
            _chatService.PropertyChanged += OnServicePropertyChanged;
            UpdateStatusLabel();

            // ADD: Read the saved preference and update our public property
            IsCopyVisible = Preferences.Default.Get("copy_feature_enabled", true);
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.PropertyChanged -= OnServicePropertyChanged;
        }

        // ADD: This event handler is triggered when the copy label is tapped
        private async void CopyLabel_Tapped(object sender, TappedEventArgs e)
        {
            // Check if the sender is a Label
            if (sender is Label tappedLabel)
            {
                // The data for this specific label is in its BindingContext
                if (tappedLabel.BindingContext is ChatMessageViewModel messageViewModel)
                {
                    // Get the text and copy to clipboard
                    await Clipboard.Default.SetTextAsync(messageViewModel.Content);

                    /*
                    tappedLabel.Text = "? Copied!";
                    await Task.Delay(1500); // Wait 1.5 seconds
                    tappedLabel.Text = "? Copy";*/
                }
            }
        }

        #region Existing Code (no changes below this line, but INotifyPropertyChanged is added)

        // ADD: The implementation for INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
                case nameof(EasyChatService.IsModelLoaded):
                    MainThread.BeginInvokeOnMainThread(UpdateStatusLabel);
                    break;
            }
        }

        private void UpdateStatusLabel()
        {
            if (_chatService.ShowHistoryNeedsModelPanel)
            {
                StatusLabel.Text = $"Previewing: {_chatService.CurrentConversation?.Name}";
            }
            else if (_chatService.IsGenerating)
            {
                StatusLabel.Text = "Generating...";
            }
            else if (_chatService.ShowLoadHistoryPanel)
            {
                StatusLabel.Text = "Previewing history. Click below to load.";
            }
            else if (_chatService.CurrentEngineState == EngineState.IDLE)
            {
                var modelName = _chatService.LoadedModel?.Name ?? "Active";
                var chatName = _chatService.CurrentConversation?.Name ?? "New Chat";
                StatusLabel.Text = $"{chatName} ({modelName})";
            }
            else if (_chatService.ShowInitialPromptPanel)
            {
                StatusLabel.Text = "No Model Loaded";
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
            string message = "In the current implementation, this process takes time. While it’s running, the chat area will be busy, and you won’t be able to start a new chat immediately.";
            bool continueWithHistory = await DisplayAlert("Load History?", message, "Continue", "New Chat");

            if (continueWithHistory)
            {
                await _chatService.LoadPendingHistoryIntoEngineAsync();
            }
            else
            {
                _chatService.StartNewConversation();
            }
        }

        private async void NavigateToModelsButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(ModelsPage));
        }

        private async void LoadDefaultButton_Clicked(object sender, EventArgs e)
        {
            if (sender is not Button button) return;

            button.IsEnabled = false;
            button.Text = "Checking...";

            var result = await _chatService.LoadDefaultModelAsync();

            switch (result)
            {
                case DefaultModelLoadResult.Success:
                    break;

                case DefaultModelLoadResult.NoDefaultModelSet:
                    bool goToModels = await DisplayAlert("No Default Model", "There is no default model set. Would you like to go to the Models page to choose one?", "Choose Model", "Cancel");
                    if (goToModels)
                    {
                        await Shell.Current.GoToAsync(nameof(ModelsPage));
                    }
                    break;

                case DefaultModelLoadResult.FailedToLoad:
                    await DisplayAlert("Error", $"Failed to load the default model. Please check its settings. Error: {_chatService.LastErrorMessage}", "OK");
                    break;
            }

            button.IsEnabled = true;
            button.Text = "Load";
        }
        #endregion
    }
}