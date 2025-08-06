// File: LoQA/Services/EasyChatService.cs
using LoQA.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace LoQA.Services
{
    public class EasyChatService : INotifyPropertyChanged, IDisposable
    {
        private readonly IEasyChatWrapper _chatEngine;
        private readonly DatabaseService _databaseService;

        private bool _isGenerating;
        private ChatHistory? _currentConversation;
        private ChatMessageViewModel? _currentAssistantMessage;
        private bool _isInitialized;
        private bool _isHistoryLoadPending;

        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true
        };

        public LlmModel? LoadedModel { get; private set; }
        public ObservableCollection<ChatMessageViewModel> CurrentMessages { get; } = new();
        public ObservableCollection<ChatHistory> ConversationList { get; } = new();

        public bool IsInitialized { get => _isInitialized; private set => SetField(ref _isInitialized, value); }
        public bool IsGenerating { get => _isGenerating; private set => SetField(ref _isGenerating, value, nameof(IsGenerating), nameof(CanUserInput)); }
        public ChatHistory? CurrentConversation { get => _currentConversation; private set => SetField(ref _currentConversation, value); }

        public bool IsHistoryLoadPending { get => _isHistoryLoadPending; private set => SetField(ref _isHistoryLoadPending, value, nameof(IsHistoryLoadPending), nameof(CanUserInput)); }

        public bool CanUserInput => IsInitialized && !IsGenerating && !IsHistoryLoadPending;

        public event PropertyChangedEventHandler? PropertyChanged;

        public EasyChatService(IEasyChatWrapper chatEngine, DatabaseService databaseService)
        {
            _chatEngine = chatEngine;
            _databaseService = databaseService;
            _chatEngine.OnTokenReceived += OnTokenReceived;
            IsInitialized = _chatEngine.IsInitialized;

            // FIX: Use the full namespace to resolve ambiguity with Java.Util.Prefs.Preferences
            string savedTemplate = Microsoft.Maui.Storage.Preferences.Get("FallbackTemplate", string.Empty);
            if (!string.IsNullOrWhiteSpace(savedTemplate))
            {
                _chatEngine.SetFallbackChatTemplate(savedTemplate);
            }
        }

        public async Task LoadModelAsync(LlmModel modelToLoad)
        {
            if (IsInitialized) await UnloadModelAsync();
            try
            {
                var modelParams = GetDefaultModelParams();
                var ctxParams = GetDefaultContextParams();
                modelParams.use_mmap = true;
                modelParams.use_mlock = false;

#if ANDROID || IOS
                ctxParams.n_ctx = 2048;
                modelParams.n_gpu_layers = 99;
#else
                ctxParams.n_ctx = 4096;
                modelParams.n_gpu_layers = 99;
#endif
                bool success = await _chatEngine.InitializeAsync(modelToLoad.FilePath, modelParams, ctxParams);
                if (success) { LoadedModel = modelToLoad; IsInitialized = true; OnPropertyChanged(nameof(LoadedModel)); OnPropertyChanged(nameof(CanUserInput)); }
                else { LoadedModel = null; IsInitialized = false; throw new Exception($"Failed to initialize model: {_chatEngine.GetLastError()}"); }
            }
            catch (Exception) { LoadedModel = null; IsInitialized = false; OnPropertyChanged(nameof(CanUserInput)); throw; }
        }

        public Task UnloadModelAsync()
        {
            if (!IsInitialized) return Task.CompletedTask;
            _chatEngine.Dispose();
            LoadedModel = null;
            IsInitialized = false;
            OnPropertyChanged(nameof(LoadedModel));
            OnPropertyChanged(nameof(CanUserInput));
            StartNewConversation();
            return Task.CompletedTask;
        }

        public async Task LoadConversationsFromDbAsync()
        {
            var conversations = await _databaseService.ListConversationsAsync();
            ConversationList.Clear();
            foreach (var convo in conversations) { ConversationList.Add(convo); }
        }

        public Task SelectConversationAsync(ChatHistory conversation)
        {
            CurrentConversation = conversation;
            CurrentMessages.Clear();

            var history = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.HistoryJson, _jsonOptions) ?? new List<ChatMessage>();
            foreach (var message in history)
            {
                CurrentMessages.Add(new ChatMessageViewModel { Role = message.Role, Content = message.Content });
            }

            IsHistoryLoadPending = true;

            _chatEngine.ClearConversation();

            OnPropertyChanged(nameof(CurrentConversation));
            return Task.CompletedTask;
        }

        public void StartNewConversation()
        {
            CurrentConversation = null;
            CurrentMessages.Clear();
            _chatEngine.ClearConversation();
            IsHistoryLoadPending = false;
            OnPropertyChanged(nameof(CurrentConversation));
        }

        public async Task LoadPendingHistoryIntoEngineAsync()
        {
            if (!IsHistoryLoadPending || CurrentConversation == null) return;

            IsGenerating = true;
            await Task.Delay(100);

            bool success = await Task.Run(() => _chatEngine.LoadFullHistory(CurrentConversation.HistoryJson));

            if (success)
            {
                IsHistoryLoadPending = false;
            }
            else
            {
                CurrentMessages.Add(new ChatMessageViewModel { Role = "assistant", Content = $"Error loading full history: {_chatEngine.GetLastError()}" });
            }
            IsGenerating = false;
        }

        public async Task SendMessageAsync(string prompt)
        {
            if (!CanUserInput || string.IsNullOrWhiteSpace(prompt)) return;

            try
            {
                IsGenerating = true;
                string originalUserPrompt = prompt;

                if (CurrentConversation == null)
                {
                    CurrentConversation = new ChatHistory { Name = prompt.Length > 40 ? prompt[..40] + "..." : prompt, HistoryJson = "[]" };
                }

                CurrentMessages.Add(new ChatMessageViewModel { Role = "user", Content = originalUserPrompt });
                _currentAssistantMessage = new ChatMessageViewModel { Role = "assistant", Content = "" };
                CurrentMessages.Add(_currentAssistantMessage);

                bool success = await _chatEngine.GenerateAsync(originalUserPrompt);

                if (success)
                {
                    if (_currentAssistantMessage != null && CurrentConversation != null)
                    {
                        var finalAssistantContent = _currentAssistantMessage.Content.Trim();
                        var history = JsonSerializer.Deserialize<List<ChatMessage>>(CurrentConversation.HistoryJson, _jsonOptions) ?? new List<ChatMessage>();
                        history.Add(new ChatMessage { Role = "user", Content = originalUserPrompt });
                        history.Add(new ChatMessage { Role = "assistant", Content = finalAssistantContent });

                        CurrentConversation.HistoryJson = JsonSerializer.Serialize(history, _jsonOptions);
                        CurrentConversation.MessageCount = history.Count;
                        await _databaseService.SaveConversationAsync(CurrentConversation);
                        UpdateSidebar(CurrentConversation);
                    }
                }
                else
                {
                    if (_currentAssistantMessage != null)
                    {
                        _currentAssistantMessage.Content += $"\n\nERROR: Generation failed. {_chatEngine.GetLastError()}";
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error during SendMessageAsync: {ex.Message}"); }
            finally { IsGenerating = false; _currentAssistantMessage = null; }
        }

        public bool SetFallbackTemplate(string template)
        {
            bool success = _chatEngine.SetFallbackChatTemplate(template);
            if (success)
            {
                // FIX: Use the full namespace to resolve ambiguity
                Microsoft.Maui.Storage.Preferences.Set("FallbackTemplate", template);
            }
            return success;
        }

        private void OnTokenReceived(string token)
        {
            if (_currentAssistantMessage != null)
            {
                if (token == "<EOS>")
                {
                    return;
                }
                MainThread.BeginInvokeOnMainThread(() => { _currentAssistantMessage.Content += token; });
            }
        }

        private void UpdateSidebar(ChatHistory updatedConversation)
        {
            var existing = ConversationList.FirstOrDefault(c => c.Id == updatedConversation.Id);
            if (existing == null) { ConversationList.Insert(0, updatedConversation); }
            else
            {
                existing.Name = updatedConversation.Name;
                existing.LastModified = updatedConversation.LastModified;
                int oldIndex = ConversationList.IndexOf(existing);
                if (oldIndex != 0) { ConversationList.Move(oldIndex, 0); }
            }
        }

        public void Dispose() { if (IsInitialized) { UnloadModelAsync().GetAwaiter().GetResult(); } GC.SuppressFinalize(this); }
        public void StopGeneration() => _chatEngine.StopGeneration();
        public void UpdateSamplingParams(ChatSamplingParams newParams) => _chatEngine.UpdateSamplingParams(newParams);
        public ChatSamplingParams GetCurrentSamplingParams() => _chatEngine.GetCurrentSamplingParams();
        public ChatModelParams GetDefaultModelParams() => _chatEngine.GetDefaultModelParams();
        public ChatContextParams GetDefaultContextParams() => _chatEngine.GetDefaultContextParams();
        public ChatSamplingParams GetDefaultSamplingParams() => _chatEngine.GetDefaultSamplingParams();
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] additionalProperties)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value; OnPropertyChanged(propertyName);
            foreach (var prop in additionalProperties)
            {
                OnPropertyChanged(prop);
            }
            return true;
        }
    }
}