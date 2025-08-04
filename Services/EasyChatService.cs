// File: LoQA/Services/EasyChatService.cs
using LoQA.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private string _pendingUserPrompt = string.Empty;

        public LlmModel? LoadedModel { get; private set; }
        public ObservableCollection<ChatMessageViewModel> CurrentMessages { get; } = new();
        public ObservableCollection<ChatHistory> ConversationList { get; } = new();

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            private set => SetField(ref _isInitialized, value);
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            private set => SetField(ref _isGenerating, value);
        }

        public ChatHistory? CurrentConversation
        {
            get => _currentConversation;
            private set => SetField(ref _currentConversation, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public EasyChatService(IEasyChatWrapper chatEngine, DatabaseService databaseService)
        {
            _chatEngine = chatEngine;
            _databaseService = databaseService;
            _chatEngine.OnTokenReceived += OnTokenReceived;
            IsInitialized = _chatEngine.IsInitialized;
        }

        public async Task LoadModelAsync(LlmModel modelToLoad)
        {
            if (IsInitialized)
            {
                await UnloadModelAsync();
            }

            try
            {
                var modelParams = GetDefaultModelParams();
                var ctxParams = GetDefaultContextParams();

                // =========================================================================
                // === THE FIX: Set boolean flags using bytes (1=true, 0=false) to     ===
                // ===          guarantee correctness for the native library.            ===
                // =========================================================================
                modelParams.use_mmap = 1;   // USE MEMORY MAPPING! This is essential.
                modelParams.use_mlock = 0; // mlock can cause issues on constrained devices.

#if ANDROID || IOS
                ctxParams.n_ctx = 2048;        // Reduce context size from 4096 to 2048 to save RAM.
                modelParams.n_gpu_layers = 25; // Reduce GPU layers. More layers = more VRAM usage.
#else
                ctxParams.n_ctx = 4096;
                modelParams.n_gpu_layers = 50;
#endif

                bool success = await _chatEngine.InitializeAsync(modelToLoad.FilePath, modelParams, ctxParams);

                if (success)
                {
                    LoadedModel = modelToLoad;
                    IsInitialized = true;
                    OnPropertyChanged(nameof(LoadedModel));
                }
                else
                {
                    LoadedModel = null;
                    IsInitialized = false;
                    throw new Exception($"Failed to initialize model: {_chatEngine.GetLastError()}");
                }
            }
            catch (Exception)
            {
                LoadedModel = null;
                IsInitialized = false;
                throw; // Re-throw the exception so the UI layer can catch it.
            }
        }

        public Task UnloadModelAsync()
        {
            if (!IsInitialized)
            {
                return Task.CompletedTask;
            }
            _chatEngine.Dispose();
            LoadedModel = null;
            IsInitialized = false;
            OnPropertyChanged(nameof(LoadedModel));
            StartNewConversation();
            return Task.CompletedTask;
        }

        public async Task LoadConversationsFromDbAsync()
        {
            var conversations = await _databaseService.ListConversationsAsync();
            ConversationList.Clear();
            foreach (var convo in conversations)
            {
                ConversationList.Add(convo);
            }
        }

        public Task LoadConversationAsync(ChatHistory conversation)
        {
            CurrentConversation = conversation;
            _chatEngine.ClearConversation();
            var history = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.HistoryJson) ?? new List<ChatMessage>();
            CurrentMessages.Clear();
            foreach (var message in history)
            {
                _chatEngine.AddHistoryMessage(message.Role, message.Content);
                CurrentMessages.Add(new ChatMessageViewModel { Role = message.Role, Content = message.Content });
            }
            OnPropertyChanged(nameof(CurrentConversation));
            return Task.CompletedTask;
        }

        public void StartNewConversation()
        {
            CurrentConversation = null;
            CurrentMessages.Clear();
            _chatEngine.ClearConversation();
            OnPropertyChanged(nameof(CurrentConversation));
        }

        public async Task SendMessageAsync(string prompt)
        {
            if (IsGenerating || string.IsNullOrWhiteSpace(prompt) || !IsInitialized)
            {
                return;
            }

            try
            {
                IsGenerating = true;
                _pendingUserPrompt = prompt;

                if (CurrentConversation == null)
                {
                    CurrentConversation = new ChatHistory { Name = prompt.Length > 40 ? prompt[..40] + "..." : prompt, HistoryJson = "[]" };
                }

                CurrentMessages.Add(new ChatMessageViewModel { Role = "user", Content = prompt });
                _currentAssistantMessage = new ChatMessageViewModel { Role = "assistant", Content = "" };
                CurrentMessages.Add(_currentAssistantMessage);

                await _chatEngine.GenerateAsync(prompt);

                if (_currentAssistantMessage != null)
                {
                    var finalAssistantContent = _currentAssistantMessage.Content.Trim();
                    var history = JsonSerializer.Deserialize<List<ChatMessage>>(CurrentConversation!.HistoryJson) ?? new List<ChatMessage>();
                    history.Add(new ChatMessage { Role = "user", Content = _pendingUserPrompt });
                    history.Add(new ChatMessage { Role = "assistant", Content = finalAssistantContent });
                    CurrentConversation.HistoryJson = JsonSerializer.Serialize(history);
                    CurrentConversation.MessageCount = history.Count;
                    await _databaseService.SaveConversationAsync(CurrentConversation);
                    UpdateSidebar(CurrentConversation);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during SendMessageAsync: {ex.Message}");
            }
            finally
            {
                IsGenerating = false;
                _currentAssistantMessage = null;
                _pendingUserPrompt = string.Empty;
            }
        }

        private void OnTokenReceived(string token)
        {
            if (_currentAssistantMessage != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _currentAssistantMessage.Content += token;
                });
            }
        }

        private void UpdateSidebar(ChatHistory updatedConversation)
        {
            var existing = ConversationList.FirstOrDefault(c => c.Id == updatedConversation.Id);
            if (existing == null)
            {
                ConversationList.Insert(0, updatedConversation);
            }
            else
            {
                existing.Name = updatedConversation.Name;
                existing.LastModified = updatedConversation.LastModified;
                int oldIndex = ConversationList.IndexOf(existing);
                if (oldIndex != 0)
                {
                    ConversationList.Move(oldIndex, 0);
                }
            }
        }

        public void Dispose()
        {
            if (IsInitialized)
            {
                UnloadModelAsync().GetAwaiter().GetResult();
            }
            GC.SuppressFinalize(this);
        }

        public void StopGeneration() => _chatEngine.StopGeneration();
        public void UpdateSamplingParams(ChatSamplingParams newParams) => _chatEngine.UpdateSamplingParams(newParams);
        public ChatSamplingParams GetCurrentSamplingParams() => _chatEngine.GetCurrentSamplingParams();
        public ChatModelParams GetDefaultModelParams() => _chatEngine.GetDefaultModelParams();
        public ChatContextParams GetDefaultContextParams() => _chatEngine.GetDefaultContextParams();
        public ChatSamplingParams GetDefaultSamplingParams() => _chatEngine.GetDefaultSamplingParams();

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}