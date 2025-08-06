using LoQA.Models;
using LoQA.Services;
using System.ComponentModel;
using System.Linq; // Added for .FirstOrDefault()

namespace LoQA.Views
{
    public partial class Sidebar : ContentView
    {
        private EasyChatService? _chatService;
        private DatabaseService? _databaseService;

        public Sidebar()
        {
            InitializeComponent();
            // The handler will be attached when the control is loaded into the UI.
            this.HandlerChanged += OnHandlerChanged;
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (Handler?.MauiContext == null)
            {
                // Unsubscribe if the handler is being detached.
                if (_chatService != null)
                {
                    _chatService.PropertyChanged -= OnServicePropertyChanged;
                }
                return;
            }

            // This is the correct place to get services.
            _databaseService = Handler.MauiContext.Services.GetService<DatabaseService>();
            var newChatService = Handler.MauiContext.Services.GetService<EasyChatService>();

            if (_chatService != newChatService)
            {
                if (_chatService != null)
                {
                    _chatService.PropertyChanged -= OnServicePropertyChanged;
                }

                _chatService = newChatService;

                if (_chatService != null)
                {
                    this.BindingContext = _chatService;
                    _chatService.PropertyChanged += OnServicePropertyChanged;
                    // Load conversations on a background thread.
                    Task.Run(() => _chatService.LoadConversationsFromDbAsync());
                    UpdateAllStates();
                }
            }
        }

        private void UpdateAllStates()
        {
            if (_chatService == null) return;

            var currentParams = _chatService.GetCurrentSamplingParams();
            TemperatureSlider.Value = currentParams.temperature;
            TemperatureValueLabel.Text = $"Current: {currentParams.temperature:F2}";
            ConversationsListView.SelectedItem = _chatService.CurrentConversation;
            // The busy state depends on both generating and pending history loads.
            UpdateBusyState(_chatService.IsGenerating || _chatService.IsHistoryLoadPending);
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_chatService == null) return;

            // Ensure UI updates happen on the main thread.
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.IsGenerating):
                    case nameof(EasyChatService.IsHistoryLoadPending):
                        UpdateBusyState(_chatService.IsGenerating || _chatService.IsHistoryLoadPending);
                        break;
                    case nameof(EasyChatService.CurrentConversation):
                        if (ConversationsListView.SelectedItem != _chatService.CurrentConversation)
                        {
                            ConversationsListView.SelectedItem = _chatService.CurrentConversation;
                        }
                        break;
                }
            });
        }

        // Renamed for clarity. Disables controls when the engine is busy.
        private void UpdateBusyState(bool isBusy)
        {
            NewChatButton.IsEnabled = !isBusy;
            ConversationsListView.IsEnabled = !isBusy;
            ModelsButton.IsEnabled = !isBusy;
            SettingsButton.IsEnabled = !isBusy;
            TemperatureSlider.IsEnabled = !isBusy;
        }

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService == null || _chatService.IsGenerating) return;

            ConversationsListView.SelectedItem = null;
            _chatService.StartNewConversation();
            if (Shell.Current is not null)
            {
                Shell.Current.FlyoutIsPresented = false;
            }
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_chatService == null || e.CurrentSelection.FirstOrDefault() is not ChatHistory selected) return;
            if (_chatService.CurrentConversation?.Id == selected.Id && !_chatService.IsHistoryLoadPending) return;

            await _chatService.SelectConversationAsync(selected);

            if (Shell.Current is not null)
            {
                Shell.Current.FlyoutIsPresented = false;
            }
        }

        private async void DeleteButton_Clicked(object sender, EventArgs e)
        {
            if ((sender as Button)?.CommandParameter is not ChatHistory convToDelete) return;
            if (_databaseService == null || _chatService == null) return;

            var page = this.GetParentPage();
            if (page == null) return;

            bool confirm = await page.DisplayAlert("Delete Chat?", $"Are you sure you want to delete '{convToDelete.Name}'?", "Delete", "Cancel");
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
            if (_chatService == null || !_chatService.IsInitialized) return;

            var currentParams = _chatService.GetCurrentSamplingParams();
            currentParams.temperature = (float)e.NewValue;
            _chatService.UpdateSamplingParams(currentParams);
        }

        private async void ModelsButton_Clicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(ModelsPage));
            }
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
        }
    }

    public static class VisualElementExtensions
    {
        public static Page? GetParentPage(this VisualElement element)
        {
            Element? parent = element.Parent;
            while (parent != null && !(parent is Page))
            {
                parent = parent.Parent;
            }
            return parent as Page;
        }
    }
}