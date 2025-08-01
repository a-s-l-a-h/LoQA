#pragma warning disable CA1416 // Validate platform compatibility

using LoQA.Services;
using System.Text;
using System.Threading.Tasks;

namespace LoQA.Views;

public partial class ChatPage : ContentPage
{
    private readonly EasyChatService _chatService;
    private readonly StringBuilder _conversationBuilder = new();
    private bool _isGenerating = false;

    // --- No changes in the constructor or GetModelPathAsync ---
    public ChatPage(EasyChatService chatService)
    {
        InitializeComponent();
        _chatService = chatService;

        _chatService.OnTokenReceived += OnTokenReceived;
        TemperatureSlider.ValueChanged += OnSamplingParamsChanged;
        MinPSlider.ValueChanged += OnSamplingParamsChanged;
    }

    private async Task<string> GetModelPathAsync()
    {
        const string modelFileName = "qwen.gguf";
        string destinationPath = Path.Combine(FileSystem.AppDataDirectory, modelFileName);

        if (File.Exists(destinationPath))
        {
            return destinationPath;
        }

        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "First launch: Copying model to local storage...";
            });

            using var stream = await FileSystem.OpenAppPackageFileAsync(modelFileName);
            using var destinationStream = File.OpenWrite(destinationPath);
            await stream.CopyToAsync(destinationStream);

            return destinationPath;
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Error copying model.";
                _conversationBuilder.Append($"\nFATAL ERROR: Could not copy model file: {ex.Message}");
                ConversationLabel.Text = _conversationBuilder.ToString();
            });
            return string.Empty;
        }
    }

    // --- No changes in InitializeButton_Clicked, OnTokenReceived, etc. ---
    private async void InitializeButton_Clicked(object sender, EventArgs e)
    {
        if (_chatService.IsInitialized)
        {
            _chatService.Dispose(); // Re-initializing
        }

        SetInitializingState(true);

        string modelPath = await GetModelPathAsync();
        if (string.IsNullOrEmpty(modelPath))
        {
            SetInitializingState(false);
            return;
        }

        var modelParams = EasyChatService.GetDefaultModelParams();
        var ctxParams = EasyChatService.GetDefaultContextParams();

        modelParams.n_gpu_layers = int.TryParse(GpuLayersEntry.Text, out var gpu) ? gpu : 0;
        ctxParams.n_ctx = int.TryParse(ContextSizeEntry.Text, out var ctx) ? ctx : 4096;

        bool success = await _chatService.InitializeAsync(modelPath, modelParams, ctxParams);

        if (success)
        {
            UpdateSamplingParameters();
            _conversationBuilder.Clear();
            _conversationBuilder.Append("Model initialized successfully. You can start chatting.");
            ConversationLabel.Text = _conversationBuilder.ToString();
            SetReadyState();
        }
        else
        {
            StatusLabel.Text = "Initialization failed. Check logs.";
            _conversationBuilder.Append("\nERROR: Could not initialize model.");
            ConversationLabel.Text = _conversationBuilder.ToString();
        }

        SetInitializingState(false);
    }

    private void OnTokenReceived(string token)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            _conversationBuilder.Append(token);
            ConversationLabel.Text = _conversationBuilder.ToString();
            await Task.Delay(50);
            await ChatScrollView.ScrollToAsync(0, ChatScrollView.ContentSize.Height, true);
        });
    }

    private async void SendButton_Clicked(object sender, EventArgs e)
    {
        if (_isGenerating || string.IsNullOrWhiteSpace(PromptEditor.Text))
            return;

        string prompt = PromptEditor.Text.Trim();
        _conversationBuilder.Append($"\n\n**You:**\n{prompt}\n\n**Assistant:**\n");
        ConversationLabel.Text = _conversationBuilder.ToString();
        PromptEditor.Text = string.Empty;

        SetGeneratingState(true);

        try
        {
            await Task.Run(() => _chatService.Generate(prompt));
        }
        catch (Exception ex)
        {
            OnTokenReceived($"\nERROR: An exception occurred: {ex.Message}");
        }
        finally
        {
            SetGeneratingState(false);
        }
    }

    private void ToggleSettingsButton_Clicked(object sender, EventArgs e)
    {
        SettingsScrollView.IsVisible = !SettingsScrollView.IsVisible;
        ToggleSettingsButton.Text = SettingsScrollView.IsVisible ? "Hide Settings" : "Show Settings";
    }

    private void OnSamplingParamsChanged(object? sender, ValueChangedEventArgs e)
    {
        if (_chatService.IsInitialized)
        {
            UpdateSamplingParameters();
            var currentParams = _chatService.GetCurrentSamplingParams();
            StatusLabel.Text = $"Ready. Temp: {currentParams.temperature:F2}, MinP: {currentParams.min_p:F2}";
        }
    }

    private void UpdateSamplingParameters()
    {
        if (!_chatService.IsInitialized) return;

        var newParams = _chatService.GetCurrentSamplingParams();
        newParams.temperature = (float)TemperatureSlider.Value;
        newParams.min_p = (float)MinPSlider.Value;
        newParams.seed = int.TryParse(SeedEntry.Text, out var seed) ? seed : -1;

        _chatService.UpdateSamplingParams(newParams);
    }

    #region Session & History (UPDATED)
    // UPDATED: Now saves both the memory state and the token history.
    private async void SaveSessionButton_Clicked(object sender, EventArgs e)
    {
        var sessionData = _chatService.ExportSessionState();
        var tokenData = _chatService.ExportTokenHistory(); // Export tokens as int[]

        if (sessionData == null || tokenData == null)
        {
            await DisplayAlert("Error", "Failed to export session state or token history.", "OK");
            return;
        }

        try
        {
            string sessionFile = Path.Combine(FileSystem.CacheDirectory, "chat_session.state");
            string tokensFile = Path.Combine(FileSystem.CacheDirectory, "chat_tokens.dat");

            await File.WriteAllBytesAsync(sessionFile, sessionData);

            // Convert int[] to byte[] for saving
            byte[] tokenBytes = new byte[tokenData.Length * sizeof(int)];
            Buffer.BlockCopy(tokenData, 0, tokenBytes, 0, tokenBytes.Length);
            await File.WriteAllBytesAsync(tokensFile, tokenBytes);

            await DisplayAlert("Success", $"Session saved successfully to cache.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Save failed: {ex.Message}", "OK");
        }
    }

    // UPDATED: Now loads both files and passes both to the import function.
    private async void LoadSessionButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            string sessionFile = Path.Combine(FileSystem.CacheDirectory, "chat_session.state");
            string tokensFile = Path.Combine(FileSystem.CacheDirectory, "chat_tokens.dat");

            if (!File.Exists(sessionFile) || !File.Exists(tokensFile))
            {
                await DisplayAlert("Not Found", "Session state or token file not found.", "OK");
                return;
            }

            // Read both files
            var sessionData = await File.ReadAllBytesAsync(sessionFile);
            var tokenBytes = await File.ReadAllBytesAsync(tokensFile);

            // Convert byte[] back to int[] for tokens
            int[] tokenData = new int[tokenBytes.Length / sizeof(int)];
            Buffer.BlockCopy(tokenBytes, 0, tokenData, 0, tokenBytes.Length);

            // This is the line that caused the error. Now it passes both required arguments.
            if (_chatService.ImportSessionState(sessionData, tokenData))
            {
                _conversationBuilder.Clear().Append("Session loaded successfully. NOTE: The visual chat history is not restored from session, only the model's internal memory. You can continue the conversation.");
                ConversationLabel.Text = _conversationBuilder.ToString();
                await DisplayAlert("Success", "Session loaded.", "OK");
            }
            else
            {
                await DisplayAlert("Error", $"Failed to import session: {_chatService.GetLastError()}", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Load failed: {ex.Message}", "OK");
        }
    }

    private void ClearHistoryButton_Clicked(object sender, EventArgs e)
    {
        _chatService.ClearConversation();
        _conversationBuilder.Clear().Append("Chat history cleared.");
        ConversationLabel.Text = _conversationBuilder.ToString();
        var p = _chatService.GetCurrentSamplingParams();
        StatusLabel.Text = $"History Cleared. Temp: {p.temperature:F2}, MinP: {p.min_p:F2}";
    }
    #endregion

    // --- No changes in UI State Management or OnDisappearing ---
    #region UI State Management
    private void SetInitializingState(bool isInitializing)
    {
        BusyIndicator.IsRunning = isInitializing;
        InitializeButton.IsEnabled = !isInitializing;
        StatusLabel.Text = isInitializing ? "Initializing model..." : "Please initialize the model.";
    }

    private void SetReadyState()
    {
        var p = _chatService.GetCurrentSamplingParams();
        StatusLabel.Text = $"Ready. Temp: {p.temperature:F2}, MinP: {p.min_p:F2}";
        PromptEditor.IsEnabled = true;
        SendButton.IsEnabled = true;
        SaveSessionButton.IsEnabled = true;
        LoadSessionButton.IsEnabled = true;

        ClearHistoryButton.IsEnabled = true;
        InitializeButton.Text = "Reload Model";
    }

    private void SetGeneratingState(bool isGenerating)
    {
        _isGenerating = isGenerating;
        BusyIndicator.IsRunning = isGenerating;
        SendButton.IsEnabled = !isGenerating;
        InitializeButton.IsEnabled = !isGenerating;
        StatusLabel.Text = isGenerating ? "Generating..." : "Ready.";
        if (!isGenerating) SetReadyState();
    }
    #endregion

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _chatService.OnTokenReceived -= OnTokenReceived;
        TemperatureSlider.ValueChanged -= OnSamplingParamsChanged;
        MinPSlider.ValueChanged -= OnSamplingParamsChanged;
    }
}