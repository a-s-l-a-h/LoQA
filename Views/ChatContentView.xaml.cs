using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class ChatContentView : ContentView
    {
        private EasyChatService _chatService;
        private bool _isUserScrolledUp = false;

        public event EventHandler ToggleSidebarRequested;

        public ChatContentView()
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

                // Sync initial state
                if (_chatService.IsInitialized) SetReadyState();
                UpdateGeneratingState(_chatService.IsGenerating);
                UpdateStatusLabel();
            }
        }

        private void OnServicePropertyChanged(object sender, PropertyChangedEventArgs e)
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

                var modelParams = _chatService.GetDefaultModelParams();
                var ctxParams = _chatService.GetDefaultContextParams();

                modelParams.n_gpu_layers = 50;
                ctxParams.n_ctx = 4096;

                await _chatService.InitializeEngineAsync(modelPath, modelParams, ctxParams);

                if (!_chatService.IsInitialized)
                {
                    await this.GetParentPage().DisplayAlert("Error", "Failed to initialize the LLaMA model.", "OK");
                    SetInitializingState(false);
                }
            }
            catch (Exception ex)
            {
                await this.GetParentPage().DisplayAlert("Fatal Error", $"An exception occurred during initialization: {ex.Message}", "OK");
                SetInitializingState(false);
            }
        }

        private void UpdateGeneratingState(bool isGenerating)
        {
            SendButton.IsVisible = !isGenerating;
            StopButton.IsVisible = isGenerating;
            PromptEditor.IsEnabled = !isGenerating;

            UpdateStatusLabel();
            if (!isGenerating && _chatService.IsInitialized)
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
                StatusLabel.Text = $"Active: {_chatService.CurrentConversation?.Name ?? "New Chat"}";
            }
            else
            {
                StatusLabel.Text = "Please initialize the model.";
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
            BusyIndicator.IsVisible = false;
            PromptEditor.Focus();
            UpdateStatusLabel();
        }

        private void ChatMessagesView_Scrolled(object sender, ItemsViewScrolledEventArgs e)
        {
            if (_chatService.CurrentMessages.Count == 0) return;
            if (e.LastVisibleItemIndex < _chatService.CurrentMessages.Count - 2)
            {
                _isUserScrolledUp = true;
            }
            else
            {
                _isUserScrolledUp = false;
            }
        }

        private void ToggleSidebarButton_Clicked(object sender, EventArgs e)
        {
            ToggleSidebarRequested?.Invoke(this, EventArgs.Empty);
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
                await MainThread.InvokeOnMainThreadAsync(async () => {
                    StatusLabel.Text = "Error copying model.";
                    if (this.GetParentPage() != null)
                        await this.GetParentPage().DisplayAlert("Model Error", $"Could not copy the required model file: {ex.Message}", "OK");
                });
                return string.Empty;
            }
        }
    }
}