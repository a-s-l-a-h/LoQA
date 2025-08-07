using LoQA.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace LoQA.Services
{
    public enum EngineState { UNINITIALIZED, IDLE, BUSY, IN_ERROR }

    public class EasyChatService : INotifyPropertyChanged, IDisposable
    {
        private readonly EasyChatEngine _engine;
        private readonly DatabaseService _databaseService;
        private ChatMessageViewModel? _currentAssistantMessage;

        private EngineState _currentEngineState = EngineState.UNINITIALIZED;
        private string _lastErrorMessage = string.Empty;
        private ulong _currentTaskId = 0;
        private bool _isHistoryLoadPending;
        private bool _isLoadingHistory;
        private bool _isGenerating;

        public LlmModel? LoadedModel { get; private set; }
        public ChatHistory? CurrentConversation { get; private set; }
        public ObservableCollection<ChatMessageViewModel> CurrentMessages { get; } = new();
        public ObservableCollection<ChatHistory> ConversationList { get; } = new();

        public EngineState CurrentEngineState { get => _currentEngineState; private set => SetField(ref _currentEngineState, value, nameof(CurrentEngineState), nameof(IsIdle), nameof(IsBusy), nameof(CanUserInput)); }
        public string LastErrorMessage { get => _lastErrorMessage; private set => SetField(ref _lastErrorMessage, value); }

        public bool IsGenerating { get => _isGenerating; private set => SetField(ref _isGenerating, value, nameof(IsGenerating), nameof(IsBusy), nameof(CanUserInput)); }
        public bool IsHistoryLoadPending { get => _isHistoryLoadPending; private set => SetField(ref _isHistoryLoadPending, value, nameof(IsHistoryLoadPending), nameof(CanUserInput)); }
        public bool IsLoadingHistory { get => _isLoadingHistory; private set => SetField(ref _isLoadingHistory, value); }

        public bool IsBusy => CurrentEngineState == EngineState.BUSY && !IsGenerating && !IsLoadingHistory;
        public bool IsIdle => CurrentEngineState == EngineState.IDLE;
        public bool CanUserInput => CurrentEngineState == EngineState.IDLE && !IsHistoryLoadPending;

        public float CurrentTemperature { get; private set; } = 0.8f;
        public float CurrentMinP { get; private set; } = 0.1f;

        public event PropertyChangedEventHandler? PropertyChanged;

        public EasyChatService(EasyChatEngine engine, DatabaseService databaseService)
        {
            _engine = engine;
            _databaseService = databaseService;
            _engine.OnTokenReceived += OnTokenReceived;
            PollStatusAsync();
        }

        public async Task LoadModelAsync(LlmModel modelToLoad)
        {
            if (CurrentEngineState != EngineState.UNINITIALIZED)
            {
                await UnloadModelAsync();
            }

            CurrentTemperature = modelToLoad.CustomTemperature ?? 0.8f;
            CurrentMinP = modelToLoad.CustomMinP ?? 0.1f;
            OnPropertyChanged(nameof(CurrentTemperature));
            OnPropertyChanged(nameof(CurrentMinP));

            var commandParams = new Dictionary<string, string>
            {
                { "command", "initialize" },
                { "model_path", modelToLoad.FilePath },
                { "n_ctx", (modelToLoad.CustomCtx ?? 4096).ToString() },
                { "n_gpu_layers", (modelToLoad.CustomGpuLayers ?? 99).ToString() },
                { "temperature", CurrentTemperature.ToString("F2") },
                { "min_p", CurrentMinP.ToString("F2") }
            };

            string command = BuildCommandString(commandParams);
            var response = _engine.InvokeCommand(command);

            if (response.TryGetValue("status", out var status) && status.GetString() == "QUEUED")
            {
                LoadedModel = modelToLoad;
                OnPropertyChanged(nameof(LoadedModel));
                await PollUntilIdleOrErrorAsync(response);
            }
            else
            {
                HandleErrorResponse(response);
            }
        }

        public Task UnloadModelAsync()
        {
            if (CurrentEngineState == EngineState.UNINITIALIZED) return Task.CompletedTask;
            AbortCurrentTask();
            _engine.InvokeCommand("command=free");
            PollStatusAsync();
            LoadedModel = null;
            OnPropertyChanged(nameof(LoadedModel));
            StartNewConversation();
            return Task.CompletedTask;
        }

        public async Task SendMessageAsync(string prompt)
        {
            if (!CanUserInput || string.IsNullOrWhiteSpace(prompt)) return;

            IsGenerating = true;

            var userMessageVm = new ChatMessageViewModel { Role = "user", Content = prompt };
            CurrentMessages.Add(userMessageVm);

            if (CurrentConversation == null)
            {
                CurrentConversation = new ChatHistory { Name = prompt.Length > 40 ? prompt[..40] + "..." : prompt };
            }

            _currentAssistantMessage = new ChatMessageViewModel { Role = "assistant", Content = "" };
            CurrentMessages.Add(_currentAssistantMessage);

            var command = $"command=generate&user_input={HttpUtility.UrlEncode(prompt)}";
            var response = _engine.InvokeCommand(command);

            if (response.TryGetValue("status", out var status) && status.GetString() == "QUEUED")
            {
                await PollUntilIdleOrErrorAsync(response);
                if (_currentAssistantMessage != null)
                {
                    await FinalizeAndSaveConversation(prompt, _currentAssistantMessage.Content);
                }
            }
            else
            {
                HandleErrorResponse(response);
            }
            _currentAssistantMessage = null;
        }

        public async Task SelectConversationAsync(ChatHistory conversation)
        {
            AbortCurrentTask();
            _engine.InvokeCommand("command=clear_context");

            CurrentConversation = conversation;
            OnPropertyChanged(nameof(CurrentConversation));

            CurrentMessages.Clear();
            var historyList = JsonSerializer.Deserialize<List<ChatMessage>>(conversation.HistoryJson) ?? new List<ChatMessage>();
            foreach (var message in historyList)
            {
                CurrentMessages.Add(new ChatMessageViewModel { Role = message.Role, Content = message.Content });
            }

            IsHistoryLoadPending = true;
            await PollStatusAsync();
        }

        public async Task LoadPendingHistoryIntoEngineAsync()
        {
            if (!IsHistoryLoadPending || CurrentConversation == null) return;

            IsLoadingHistory = true;

            string customHistoryFormat = ConvertJsonHistoryToCustomFormat(CurrentConversation.HistoryJson);
            var command = $"command=load_history&history_text={HttpUtility.UrlEncode(customHistoryFormat)}";
            var response = _engine.InvokeCommand(command);

            if (response.TryGetValue("status", out var status) && status.GetString() == "QUEUED")
            {
                await PollUntilIdleOrErrorAsync(response);
                if (CurrentEngineState == EngineState.IDLE)
                {
                    IsHistoryLoadPending = false;
                }
            }
            else
            {
                HandleErrorResponse(response);
            }
            IsLoadingHistory = false;
        }

        public void StartNewConversation()
        {
            AbortCurrentTask();
            _engine.InvokeCommand("command=clear_context");
            CurrentConversation = null;
            CurrentMessages.Clear();
            OnPropertyChanged(nameof(CurrentConversation));

            IsHistoryLoadPending = false;
            IsLoadingHistory = false;
            IsGenerating = false;

            PollStatusAsync();
        }

        public void AbortCurrentTask()
        {
            if (CurrentEngineState == EngineState.BUSY && _currentTaskId > 0)
            {
                _engine.InvokeCommand($"command=abort&task_id={_currentTaskId}");
            }
        }

        public void UpdateSamplingParams(float temp, float minP)
        {
            if (!IsIdle) return;
            var command = $"command=set_parameters&temperature={temp:F2}&min_p={minP:F2}";
            _engine.InvokeCommand(command);

            CurrentTemperature = temp;
            CurrentMinP = minP;
            OnPropertyChanged(nameof(CurrentTemperature));
            OnPropertyChanged(nameof(CurrentMinP));
        }

        private async Task PollUntilIdleOrErrorAsync(Dictionary<string, JsonElement> initialResponse)
        {
            if (initialResponse.TryGetValue("task_id", out var idElement) && idElement.TryGetUInt64(out var taskId))
            {
                _currentTaskId = taskId;
            }

            await PollStatusAsync();

            while (CurrentEngineState == EngineState.BUSY)
            {
                await Task.Delay(200);
                await PollStatusAsync();
            }

            _currentTaskId = 0;
        }

        private Task PollStatusAsync()
        {
            var statusResponse = _engine.InvokeCommand("command=get_status");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                UpdateStateFromResponse(statusResponse);
            });
            return Task.CompletedTask;
        }

        private void UpdateStateFromResponse(Dictionary<string, JsonElement> response)
        {
            if (!response.TryGetValue("state", out var stateElement))
            {
                CurrentEngineState = EngineState.IN_ERROR;
                LastErrorMessage = "Invalid status response from engine.";
                return;
            }

            var previousState = CurrentEngineState;
            CurrentEngineState = stateElement.GetString() switch
            {
                "UNINITIALIZED" => EngineState.UNINITIALIZED,
                "IDLE" => EngineState.IDLE,
                "BUSY" => EngineState.BUSY,
                "ERROR" => EngineState.IN_ERROR,
                _ => EngineState.IN_ERROR
            };

            if (CurrentEngineState == EngineState.IN_ERROR && response.TryGetValue("message", out var msgElement))
            {
                LastErrorMessage = msgElement.GetString() ?? "Unknown error.";
            }
            else
            {
                LastErrorMessage = string.Empty;
            }

            if (previousState == EngineState.BUSY && CurrentEngineState != EngineState.BUSY)
            {
                if (IsGenerating) IsGenerating = false;
                if (IsLoadingHistory) IsLoadingHistory = false;
            }
        }

        private void HandleErrorResponse(Dictionary<string, JsonElement> response)
        {
            CurrentEngineState = EngineState.IN_ERROR;
            if (response.TryGetValue("message", out var message))
            {
                LastErrorMessage = message.GetString() ?? "An unknown error occurred.";
            }
            Debug.WriteLine($"Engine Error: {LastErrorMessage}");
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

        private async Task FinalizeAndSaveConversation(string userPrompt, string assistantResponse)
        {
            if (CurrentConversation == null) return;

            var options = new JsonSerializerOptions { WriteIndented = true };
            var history = JsonSerializer.Deserialize<List<ChatMessage>>(CurrentConversation.HistoryJson, options) ?? new List<ChatMessage>();
            history.Add(new ChatMessage { Role = "user", Content = userPrompt });
            history.Add(new ChatMessage { Role = "assistant", Content = assistantResponse.Trim() });

            CurrentConversation.HistoryJson = JsonSerializer.Serialize(history, options);
            CurrentConversation.MessageCount = history.Count;
            await _databaseService.SaveConversationAsync(CurrentConversation);

            UpdateSidebar(CurrentConversation);
        }

        private string ConvertJsonHistoryToCustomFormat(string jsonHistory)
        {
            var messages = JsonSerializer.Deserialize<List<ChatMessage>>(jsonHistory) ?? new List<ChatMessage>();
            StringBuilder sb = new();
            foreach (var msg in messages)
            {
                string roleTag = msg.Role.ToUpper() == "USER" ? "[EASYCHAT_USER]" : "[EASYCHAT_ASSISTANT]";
                sb.AppendLine(roleTag);
                sb.AppendLine(msg.Content);
                sb.AppendLine("[EASYCHAT_MSG_END]");
            }
            return sb.ToString();
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

        private string BuildCommandString(Dictionary<string, string> parameters)
        {
            var parts = parameters.Select(kvp => $"{kvp.Key}={HttpUtility.UrlEncode(kvp.Value)}");
            return string.Join("&", parts);
        }

        private void OnTokenReceived(string token)
        {
            if (_currentAssistantMessage != null)
            {
                if (token is "<EOS>" or "<STOPPED>") return;
                MainThread.BeginInvokeOnMainThread(() => { _currentAssistantMessage.Content += token; });
            }
        }

        public void Dispose()
        {
            _engine.Dispose();
            GC.SuppressFinalize(this);
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, params string[] additionalProperties)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            foreach (var prop in additionalProperties)
            {
                OnPropertyChanged(prop);
            }
            return true;
        }
    }
}