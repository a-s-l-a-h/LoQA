#pragma warning disable CA1416

using LoQA.Models;
using LoQA.Services;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;

namespace LoQA.Views
{
    public partial class ChatPage : ContentPage
    {
        private readonly EasyChatService _chatService;
        private readonly DatabaseService _databaseService;

        private ChatHistory? _currentConversation;
        private readonly ObservableCollection<ChatHistory> _conversationList = new();

        private readonly StringBuilder _currentResponseBuilder = new();
        private bool _isGenerating = false;
        private bool _isSidebarVisible = true;

        public ChatPage(EasyChatService chatService, DatabaseService databaseService)
        {
            InitializeComponent();
            _chatService = chatService;
            _databaseService = databaseService;

            ConversationsListView.ItemsSource = _conversationList;
            _chatService.OnTokenReceived += OnTokenReceived;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _databaseService.InitAsync();
            await LoadConversationsFromDb();
        }

        // =============================================================
        // BUG FIX: Initialization logic is now wrapped in a try/finally
        // to ensure the UI state is always reset.
        // =============================================================
        private async void InitializeButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService.IsInitialized)
            {
                await DisplayAlert("Initialized", "The model is already running.", "OK");
                return;
            }

            SetInitializingState(true);
            try
            {
                string modelPath = await GetModelPathAsync();
                if (string.IsNullOrEmpty(modelPath)) return;

                var modelParams = EasyChatService.GetDefaultModelParams();
                var ctxParams = EasyChatService.GetDefaultContextParams();
                modelParams.n_gpu_layers = 50;
                ctxParams.n_ctx = 4096;

                bool success = await _chatService.InitializeAsync(modelPath, modelParams, ctxParams);

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
                    await DisplayAlert("Error", "Failed to initialize the LLaMA model.", "OK");
                }
            }
            finally
            {
                // This will run whether initialization succeeds or fails, unlocking the UI.
                SetInitializingState(false);
            }
        }

        // --- Other methods remain largely the same, but are included for completeness ---

        private void OnTokenReceived(string token)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _currentResponseBuilder.Append(token);
                // Directly append to the editor for real-time feel
                ConversationEditor.Text += token;
            });
        }

        private async Task LoadConversationsFromDb()
        {
            var conversations = await _databaseService.ListConversationsAsync();
            _conversationList.Clear();
            foreach (var convo in conversations) _conversationList.Add(convo);
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not ChatHistory selectedConversation) return;
            if (selectedConversation.Id == _currentConversation?.Id) return;
            await LoadConversation(selectedConversation);
        }

        private async Task LoadConversation(ChatHistory conversation)
        {
            _currentConversation = conversation;
            _chatService.ClearConversation();
            var history = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.HistoryJson) ?? new List<ChatMessage>();
            foreach (var message in history) _chatService.AddHistoryMessage(message.Role, message.Content);
            RenderConversation();
            StatusLabel.Text = $"Active: {conversation.Name}";
        }

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            ConversationsListView.SelectedItem = null;
            _currentConversation = null;
            _chatService.ClearConversation();
            ConversationEditor.Text = "New chat session started. Type your message below.";
            StatusLabel.Text = "New Chat";
            PromptEditor.Focus();
        }

        private async void SendButton_Clicked(object sender, EventArgs e)
        {
            if (_isGenerating || string.IsNullOrWhiteSpace(PromptEditor.Text)) return;

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
                history = JsonSerializer.Deserialize<List<ChatMessage>>(_currentConversation.HistoryJson) ?? new List<ChatMessage>();
            }

            history.Add(new ChatMessage { Role = "user", Content = prompt });
            _currentConversation.HistoryJson = JsonSerializer.Serialize(history); // Save user prompt immediately
            RenderConversation(); // Show user prompt immediately

            _currentResponseBuilder.Clear();

            try
            {
                _chatService.AddHistoryMessage("user", prompt);
                await Task.Run(() => _chatService.Generate(prompt));

                history.Add(new ChatMessage { Role = "assistant", Content = _currentResponseBuilder.ToString().Trim() });
                _currentConversation.HistoryJson = JsonSerializer.Serialize(history);
                _currentConversation.MessageCount = history.Count;

                await _databaseService.SaveConversationAsync(_currentConversation);
                UpdateConversationInSidebar(_currentConversation, isNewChat);
            }
            finally
            {
                SetGeneratingState(false);
                RenderConversation(); // Final render
            }
        }

        private void UpdateConversationInSidebar(ChatHistory updatedConversation, bool isNew)
        {
            if (!isNew)
            {
                var existing = _conversationList.FirstOrDefault(c => c.Id == updatedConversation.Id);
                if (existing != null) _conversationList.Remove(existing);
            }
            _conversationList.Insert(0, updatedConversation);
            ConversationsListView.SelectedItem = updatedConversation;
        }

        private async void DeleteConversation_Invoked(object? sender, EventArgs e)
        {
            if ((sender as SwipeItem)?.CommandParameter is not ChatHistory convToDelete) return;
            await _databaseService.DeleteConversationAsync(convToDelete.Id);
            _conversationList.Remove(convToDelete);
            if (_currentConversation?.Id == convToDelete.Id) NewChatButton_Clicked(this, EventArgs.Empty);
        }

        private void RenderConversation()
        {
            if (_currentConversation == null) return;
            var history = JsonSerializer.Deserialize<List<ChatMessage>>(_currentConversation.HistoryJson) ?? new List<ChatMessage>();
            var sb = new StringBuilder();
            foreach (var msg in history)
            {
                sb.AppendLine($"**{(msg.Role == "user" ? "You" : "Assistant")}:**");
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }
            ConversationEditor.Text = sb.ToString();
        }

        private void SetInitializingState(bool isInitializing)
        {
            StatusLabel.Text = isInitializing ? "Initializing model..." : "Please initialize the model.";
            BusyIndicator.IsRunning = isInitializing;
            InitializeButton.IsEnabled = !isInitializing;
        }

        private void SetReadyState()
        {
            StatusLabel.Text = "Ready";
            InputGrid.IsEnabled = true;
            InitializeButton.IsEnabled = false;
            InitializeButton.Text = "Initialized";
        }

        private void SetGeneratingState(bool isGenerating)
        {
            _isGenerating = isGenerating;
            BusyIndicator.IsRunning = isGenerating;
            NewChatButton.IsEnabled = !isGenerating;
            ConversationsListView.IsEnabled = !isGenerating;
            if (isGenerating) StatusLabel.Text = "Generating...";
        }

        private void ToggleSidebarButton_Clicked(object sender, EventArgs e)
        {
            _isSidebarVisible = !_isSidebarVisible;
            SidebarColumn.Width = _isSidebarVisible ? new GridLength(300) : new GridLength(0);
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
                await MainThread.InvokeOnMainThreadAsync(() => { StatusLabel.Text = $"Error copying model: {ex.Message}"; });
                return string.Empty;
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.OnTokenReceived -= OnTokenReceived;
        }
    }
}