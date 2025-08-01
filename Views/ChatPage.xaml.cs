#pragma warning disable CA1416

using LoQA.Models;
using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly EasyChatService _chatService;
        private readonly DatabaseService _databaseService; // Keep for delete
        private bool _isUserScrolledUp = false;
        private bool _isProgrammaticallyChangingSelection = false;
        private bool _isSidebarVisible = true;

        public ChatPage(EasyChatService chatService, DatabaseService databaseService)
        {
            InitializeComponent();
            _chatService = chatService;
            _databaseService = databaseService;

            this.BindingContext = _chatService;
            ConversationsListView.ItemsSource = _chatService.ConversationList;
            ChatMessagesView.ItemsSource = _chatService.CurrentMessages;

            _chatService.PropertyChanged += OnServicePropertyChanged;
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.IsGenerating):
                        UpdateGeneratingState(_chatService.IsGenerating);
                        break;
                    case nameof(EasyChatService.CurrentConversation):
                        UpdateStatusLabel();
                        break;
                    case nameof(EasyChatService.IsInitialized):
                        if (_chatService.IsInitialized) SetReadyState();
                        break;
                }
            });
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _chatService.LoadConversationsFromDbAsync();
        }

        private async void InitializeButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService.IsInitialized) return;
            SetInitializingState(true);

            try
            {
                string modelPath = await GetModelPathAsync();
                if (string.IsNullOrEmpty(modelPath))
                {
                    SetInitializingState(false);
                    return;
                }

                // --- FIXED: Correctly get params from the service ---
                var modelParams = _chatService.GetDefaultModelParams();
                var ctxParams = _chatService.GetDefaultContextParams();

                modelParams.n_gpu_layers = 50; // Example
                ctxParams.n_ctx = 4096;       // Example

                await _chatService.InitializeEngineAsync(modelPath, modelParams, ctxParams);

                if (_chatService.IsInitialized)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        SetReadyState();
                        if (_chatService.ConversationList.Any())
                        {
                            ConversationsListView.SelectedItem = _chatService.ConversationList.First();
                        }
                        else
                        {
                            _chatService.StartNewConversation();
                        }
                    });
                }
                else
                {
                    await DisplayAlert("Error", "Failed to initialize the LLaMA model.", "OK");
                    SetInitializingState(false);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Fatal Error", $"An exception occurred during initialization: {ex.Message}", "OK");
                SetInitializingState(false);
            }
        }
        // ... (The rest of ChatPage.xaml.cs remains unchanged from the previous answer)

        #region Unchanged UI Logic
        // --- UI STATE MANAGEMENT ---
        private void UpdateGeneratingState(bool isGenerating)
        {
            SendButton.IsVisible = !isGenerating;
            StopButton.IsVisible = isGenerating;
            PromptEditor.IsEnabled = !isGenerating;
            NewChatButton.IsEnabled = !isGenerating;
            ConversationsListView.IsEnabled = !isGenerating;
            SettingsButton.IsEnabled = !isGenerating;
            TemperatureSlider.IsEnabled = !isGenerating;

            UpdateStatusLabel();
            if (!isGenerating)
            {
                PromptEditor.Focus();
            }
        }

        private void UpdateStatusLabel()
        {
            if (_chatService.IsGenerating)
            {
                StatusLabel.Text = "Generating...";
            }
            else if (_chatService.IsInitialized)
            {
                StatusLabel.Text = $"Active: {_chatService.CurrentConversation?.Name ?? "New Chat"}";
            }
            else
            {
                StatusLabel.Text = "Please initialize the model.";
            }
        }

        // --- EVENT HANDLERS ---
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

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService.IsGenerating) return;
            ConversationsListView.SelectedItem = null;
            _chatService.StartNewConversation();
            PromptEditor.Focus();
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isProgrammaticallyChangingSelection || e.CurrentSelection.FirstOrDefault() is not ChatHistory selected) return;
            if (_chatService.CurrentConversation?.Id == selected.Id) return;

            await _chatService.LoadConversationAsync(selected);

            _isUserScrolledUp = false;
            await Task.Delay(100);
            ScrollToLastMessage();
        }

        private async void DeleteConversation_Invoked(object? sender, EventArgs e)
        {
            if ((sender as SwipeItem)?.CommandParameter is not ChatHistory convToDelete) return;
            bool confirm = await DisplayAlert("Delete Chat?", $"Are you sure you want to delete '{convToDelete.Name}'?", "Delete", "Cancel");
            if (!confirm) return;

            await _databaseService.DeleteConversationAsync(convToDelete.Id);
            _chatService.ConversationList.Remove(convToDelete);

            if (_chatService.CurrentConversation?.Id == convToDelete.Id)
            {
                _chatService.StartNewConversation();
            }
        }

        private void TemperatureSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            TemperatureValueLabel.Text = $"Current: {e.NewValue:F2}";
            if (!_chatService.IsInitialized) return;
            var currentParams = _chatService.GetCurrentSamplingParams();
            currentParams.temperature = (float)e.NewValue;
            _chatService.UpdateSamplingParams(currentParams);
        }

        #region UI Helpers
        private void SetInitializingState(bool isInitializing)
        {
            StatusLabel.Text = isInitializing ? "Initializing model..." : "Please initialize the model.";
            BusyIndicator.IsRunning = isInitializing;
            BusyIndicator.IsVisible = isInitializing;
            InitializeButton.IsEnabled = !isInitializing;
            InputGrid.IsEnabled = false;
        }

        private void SetReadyState()
        {
            InitializeButton.IsEnabled = false;
            InitializeButton.Text = "Initialized";
            InputGrid.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            PromptEditor.Focus();
            UpdateStatusLabel();
        }

        private void ChatMessagesView_Scrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            if (e.LastVisibleItemIndex < _chatService.CurrentMessages.Count - 2)
            {
                _isUserScrolledUp = true;
            }
            else if (e.LastVisibleItemIndex >= _chatService.CurrentMessages.Count - 1)
            {
                _isUserScrolledUp = false;
            }
        }

        private void ScrollToLastMessage()
        {
            if (!_isUserScrolledUp && _chatService.CurrentMessages.Any())
            {
                ChatMessagesView.ScrollTo(_chatService.CurrentMessages.Last(), position: ScrollToPosition.End, animate: false);
            }
        }

        private void ToggleSidebarButton_Clicked(object sender, EventArgs e)
        {
            _isSidebarVisible = !_isSidebarVisible;
            SidebarColumn.Width = _isSidebarVisible ? new GridLength(320) : new GridLength(0);
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///SettingsPage");
        }

        private async Task<string> GetModelPathAsync()
        {
            const string modelFileName = "qwen.gguf";
            string destinationPath = Path.Combine(FileSystem.AppDataDirectory, modelFileName);

            if (File.Exists(destinationPath)) return destinationPath;
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() => { StatusLabel.Text = "First launch: Copying model..."; });
                using var stream = await FileSystem.OpenAppPackageFileAsync(modelFileName);
                using var destinationStream = File.OpenWrite(destinationPath);
                await stream.CopyToAsync(destinationStream);
                return destinationPath;
            }
            catch (Exception ex)
            {
                await MainThread.InvokeOnMainThreadAsync(() => {
                    StatusLabel.Text = "Error copying model.";
                    DisplayAlert("Model Error", $"Could not copy the required model file: {ex.Message}", "OK");
                });
                return string.Empty;
            }
        }
        #endregion
        #endregion
    }
}