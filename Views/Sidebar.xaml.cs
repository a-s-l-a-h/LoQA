#pragma warning disable CA1416 // Validate platform compatibility

using LoQA.Models;
using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class Sidebar : ContentView
    {
        private EasyChatService? _chatService;
        private DatabaseService? _databaseService;

        public Sidebar()
        {
            InitializeComponent();
            this.HandlerChanged += OnHandlerChanged;
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (Handler?.MauiContext == null)
            {
                if (_chatService != null) _chatService.PropertyChanged -= OnServicePropertyChanged;
                return;
            }

            _databaseService = Handler.MauiContext.Services.GetService<DatabaseService>();
            var newChatService = Handler.MauiContext.Services.GetService<EasyChatService>();

            if (_chatService != newChatService)
            {
                if (_chatService != null) _chatService.PropertyChanged -= OnServicePropertyChanged;

                _chatService = newChatService;

                if (_chatService != null)
                {
                    this.BindingContext = _chatService;
                    _chatService.PropertyChanged += OnServicePropertyChanged;
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
            UpdateGeneratingState(_chatService.IsGenerating);
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_chatService == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.IsGenerating):
                        UpdateGeneratingState(_chatService.IsGenerating);
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

        // MODIFICATION: Update this method to disable both new buttons
        private void UpdateGeneratingState(bool isGenerating)
        {
            NewChatButton.IsEnabled = !isGenerating;
            ConversationsListView.IsEnabled = !isGenerating;
            ModelsButton.IsEnabled = !isGenerating; // <-- Changed
            SettingsButton.IsEnabled = !isGenerating; // <-- Changed
            TemperatureSlider.IsEnabled = !isGenerating;
        }

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService == null || _chatService.IsGenerating) return;
            ConversationsListView.SelectedItem = null;
            _chatService.StartNewConversation();
            if (Shell.Current is not null) Shell.Current.FlyoutIsPresented = false;
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_chatService == null || e.CurrentSelection.FirstOrDefault() is not ChatHistory selected) return;
            if (_chatService.CurrentConversation?.Id == selected.Id) return;
            await _chatService.LoadConversationAsync(selected);
            if (Shell.Current is not null) Shell.Current.FlyoutIsPresented = false;
        }

        private async void DeleteConversation_Invoked(object? sender, EventArgs e)
        {
            if ((sender as SwipeItem)?.CommandParameter is not ChatHistory convToDelete) return;
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

        // MODIFICATION: Replaced old SettingsButton_Clicked with two new handlers
        private async void ModelsButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(ModelsPage));
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(SettingsPage));
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