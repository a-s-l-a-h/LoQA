#pragma warning disable CA1416 // Disable platform compatibility warnings

using LoQA.Models;
using LoQA.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;

namespace LoQA.Views
{
    public partial class ChatPage : ContentPage
    {
        // --- AUTO-SCROLL FIELDS ---
        private bool _isUserScrolledUp = false;
        private bool _isProgrammaticallyChangingSelection = false;

        private readonly EasyChatService _chatService;
        private readonly DatabaseService _databaseService;

        private ChatHistory? _currentConversation;
        private readonly ObservableCollection<ChatHistory> _conversationList = new();
        private readonly ObservableCollection<ChatMessageViewModel> _currentMessages = new();

        private bool _isGenerating = false;
        private bool _isSidebarVisible = true;
        private ChatMessageViewModel? _currentAssistantMessage;

        public ChatPage(EasyChatService chatService, DatabaseService databaseService)
        {
            InitializeComponent();
            _chatService = chatService;
            _databaseService = databaseService;

            ConversationsListView.ItemsSource = _conversationList;
            ChatMessagesView.ItemsSource = _currentMessages;

            _chatService.OnTokenReceived += OnTokenReceived;

            UpdateSamplingParameters();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _databaseService.InitAsync();
            await LoadConversationsFromDb();
        }

        // --- NEW: AUTO-SCROLL LOGIC ---
        private void ChatMessagesView_Scrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            // A simple threshold to detect if the user has scrolled up significantly
            const double scrollThreshold = 100.0;

            // This logic checks if the user has scrolled up away from the last item
            if (e.LastVisibleItemIndex < _currentMessages.Count - 2 && e.VerticalDelta < -scrollThreshold)
            {
                _isUserScrolledUp = true;
            }
            // If the user scrolls back down to the very end, re-enable auto-scroll
            else if (e.LastVisibleItemIndex >= _currentMessages.Count - 1)
            {
                _isUserScrolledUp = false;
            }
        }

        private void ScrollToLastMessage()
        {
            // Only auto-scroll if the user hasn't scrolled up manually
            if (!_isUserScrolledUp && _currentMessages.Any())
            {
                var lastMessage = _currentMessages.Last();
                ChatMessagesView.ScrollTo(lastMessage, position: ScrollToPosition.End, animate: true);
            }
        }

        // --- FLICKER FIX: REWRITTEN SIDEBAR UPDATE LOGIC ---
        private void UpdateConversationInSidebar(ChatHistory updatedConversation, bool isNew)
        {
            _isProgrammaticallyChangingSelection = true;

            if (isNew)
            {
                _conversationList.Insert(0, updatedConversation);
            }
            else
            {
                var existing = _conversationList.FirstOrDefault(c => c.Id == updatedConversation.Id);
                if (existing != null)
                {
                    // Update properties in-place to prevent the UI from recreating the item
                    existing.Name = updatedConversation.Name;
                    existing.LastModified = updatedConversation.LastModified;

                    // Move the updated item to the top of the list
                    int oldIndex = _conversationList.IndexOf(existing);
                    if (oldIndex != 0)
                    {
                        _conversationList.Move(oldIndex, 0);
                    }
                }
            }

            // Re-select the item in the list
            ConversationsListView.SelectedItem = _conversationList.First(c => c.Id == updatedConversation.Id);

            _isProgrammaticallyChangingSelection = false;
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            if (_isGenerating || string.IsNullOrWhiteSpace(PromptEditor.Text) || !_chatService.IsInitialized) return;

            string prompt = PromptEditor.Text.Trim();
            PromptEditor.Text = string.Empty;

            SetGeneratingState(true);

            List<ChatMessage> history;
            bool isNewChat = _currentConversation == null;

            if (isNewChat)
            {
                _currentConversation = new ChatHistory { Name = prompt.Length > 40 ? prompt[..40] + "..." : prompt };
                history = new List<ChatMessage>();
            }
            else
            {
                history = JsonSerializer.Deserialize<List<ChatMessage>>(_currentConversation!.HistoryJson) ?? new List<ChatMessage>();
            }

            var userMessage = new ChatMessage { Role = "user", Content = prompt };
            history.Add(userMessage);
            _currentMessages.Add(new ChatMessageViewModel { Role = userMessage.Role, Content = userMessage.Content });
            ScrollToLastMessage(); // Scroll after user sends

            _currentAssistantMessage = new ChatMessageViewModel { Role = "assistant", Content = "" };
            _currentMessages.Add(_currentAssistantMessage);

            _currentConversation.HistoryJson = JsonSerializer.Serialize(history);

            try
            {
                UpdateSamplingParameters();
                _chatService.AddHistoryMessage("user", prompt);

                await Task.Run(() => _chatService.Generate(prompt));

                if (_currentAssistantMessage != null && _currentConversation != null)
                {
                    var finalAssistantContent = _currentAssistantMessage.Content.Trim();
                    history.Add(new ChatMessage { Role = "assistant", Content = finalAssistantContent });
                    _currentConversation.HistoryJson = JsonSerializer.Serialize(history);
                    _currentConversation.MessageCount = history.Count;

                    await _databaseService.SaveConversationAsync(_currentConversation);

                    // This now calls the improved, flicker-free method
                    UpdateConversationInSidebar(_currentConversation, isNewChat);
                }
            }
            finally
            {
                _currentAssistantMessage = null;
                SetGeneratingState(false);
            }
        }

        private void OnTokenReceived(string token)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_currentAssistantMessage != null)
                {
                    _currentAssistantMessage.Content += token;
                    ScrollToLastMessage(); // Scroll as new tokens arrive
                }
            });
        }

        private void SetGeneratingState(bool isGenerating)
        {
            _isGenerating = isGenerating;
            SendButton.IsVisible = !isGenerating;
            StopButton.IsVisible = isGenerating;
            PromptEditor.IsEnabled = !isGenerating;

            NewChatButton.IsEnabled = !isGenerating;
            ConversationsListView.IsEnabled = !isGenerating;
            SettingsButton.IsEnabled = !isGenerating;
            TemperatureSlider.IsEnabled = !isGenerating;

            if (isGenerating)
            {
                StatusLabel.Text = "Generating...";
            }
            else
            {
                StatusLabel.Text = $"Active: {_currentConversation?.Name ?? "New Chat"}";
                PromptEditor.Focus();
            }
        }

        // --- No changes to the methods below this line, but included for completeness ---

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isProgrammaticallyChangingSelection) return;
            if (e.CurrentSelection.FirstOrDefault() is not ChatHistory selectedConversation) return;
            if (_currentConversation?.Id == selectedConversation.Id) return;
            await LoadConversation(selectedConversation);
        }

        private async Task LoadConversation(ChatHistory conversation)
        {
            _currentConversation = conversation;
            _chatService.ClearConversation();

            var history = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.HistoryJson) ?? new List<ChatMessage>();
            foreach (var message in history)
            {
                _chatService.AddHistoryMessage(message.Role, message.Content);
            }

            RenderConversationAsBubbles();
            StatusLabel.Text = $"Active: {conversation.Name}";

            // When loading a new conversation, reset the scroll state
            _isUserScrolledUp = false;
            await Task.Delay(100); // Give the UI a moment to render
            ScrollToLastMessage();
        }

        private void RenderConversationAsBubbles()
        {
            _currentMessages.Clear();
            if (_currentConversation == null) return;
            var history = JsonSerializer.Deserialize<List<ChatMessage>>(_currentConversation.HistoryJson) ?? new List<ChatMessage>();
            foreach (var msg in history)
            {
                _currentMessages.Add(new ChatMessageViewModel { Role = msg.Role, Content = msg.Content });
            }
        }

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_isGenerating) return;
            ConversationsListView.SelectedItem = null;
            _currentConversation = null;
            _chatService.ClearConversation();
            _currentMessages.Clear();
            StatusLabel.Text = "New Chat";
            PromptEditor.Focus();
        }

        // (Other methods like InitializeButton_Clicked, SettingsButton_Clicked, etc. remain the same)
        #region Unchanged Methods
        private async void InitializeButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService.IsInitialized)
            {
                await DisplayAlert("Initialized", "The model is already running.", "OK");
                return;
            }

            SetInitializingState(true);
            InputGrid.IsEnabled = false;

            try
            {
                string modelPath = await GetModelPathAsync();
                if (string.IsNullOrEmpty(modelPath))
                {
                    SetInitializingState(false);
                    return;
                }

                var modelParams = EasyChatService.GetDefaultModelParams();
                var ctxParams = EasyChatService.GetDefaultContextParams();
                modelParams.n_gpu_layers = 50;
                ctxParams.n_ctx = 4096;

                bool success = await Task.Run(() => _chatService.InitializeAsync(modelPath, modelParams, ctxParams));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (success)
                    {
                        SetReadyState();
                        if (_conversationList.Any())
                        {
                            ConversationsListView.SelectedItem = _conversationList.First();
                        }
                        else
                        {
                            NewChatButton_Clicked(this, EventArgs.Empty);
                        }
                    }
                    else
                    {
                        StatusLabel.Text = "Initialization failed. See logs.";
                        DisplayAlert("Error", "Failed to initialize the LLaMA model.", "OK");
                        SetInitializingState(false);
                    }
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    StatusLabel.Text = "An error occurred.";
                    DisplayAlert("Fatal Error", $"An exception occurred during initialization: {ex.Message}", "OK");
                    SetInitializingState(false);
                });
            }
        }

        private void SetInitializingState(bool isInitializing)
        {
            StatusLabel.Text = isInitializing ? "Initializing model..." : "Please initialize the model.";
            BusyIndicator.IsRunning = isInitializing;
            BusyIndicator.IsVisible = isInitializing;
            InitializeButton.IsEnabled = !isInitializing;
        }

        private void SetReadyState()
        {
            StatusLabel.Text = "Ready";
            InitializeButton.IsEnabled = false;
            InitializeButton.Text = "Initialized";
            InputGrid.IsEnabled = true;
            PromptEditor.IsEnabled = true;
            BusyIndicator.IsRunning = false;
            BusyIndicator.IsVisible = false;

            PromptEditor.Focus();
        }

        private void StopButton_Clicked(object sender, EventArgs e)
        {
            if (!_isGenerating) return;
            _chatService.StopGeneration();
        }

        private async Task LoadConversationsFromDb()
        {
            var conversations = await _databaseService.ListConversationsAsync();
            _conversationList.Clear();
            foreach (var convo in conversations) _conversationList.Add(convo);
        }

        private async void DeleteConversation_Invoked(object? sender, EventArgs e)
        {
            if ((sender as SwipeItem)?.CommandParameter is not ChatHistory convToDelete) return;

            bool confirm = await DisplayAlert("Delete Chat?", $"Are you sure you want to delete '{convToDelete.Name}'?", "Delete", "Cancel");
            if (!confirm) return;

            await _databaseService.DeleteConversationAsync(convToDelete.Id);
            _conversationList.Remove(convToDelete);

            if (_currentConversation?.Id == convToDelete.Id)
            {
                NewChatButton_Clicked(this, EventArgs.Empty);
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

        private void TemperatureSlider_ValueChanged(object sender, ValueChangedEventArgs e)
        {
            if (TemperatureValueLabel != null)
            {
                TemperatureValueLabel.Text = $"Current: {e.NewValue:F2}";
            }
            if (_chatService.IsInitialized)
            {
                UpdateSamplingParameters();
            }
        }

        private void UpdateSamplingParameters()
        {
            if (!_chatService.IsInitialized) return;

            var currentParams = _chatService.GetCurrentSamplingParams();
            currentParams.temperature = (float)TemperatureSlider.Value;
            _chatService.UpdateSamplingParams(currentParams);
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

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.OnTokenReceived -= OnTokenReceived;
        }
        #endregion
    }
}