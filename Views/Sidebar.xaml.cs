// C:\MYWORLD\Projects\LoQA\LoQA\Views\Sidebar.xaml.cs
using LoQA.Models;
using LoQA.Services;
using System.ComponentModel;
using System.Linq;

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
                    Task.Run(() => _chatService.LoadConversationsFromDbAsync());
                    UpdateAllStates();
                }
            }
        }

        private void UpdateAllStates()
        {
            if (_chatService == null) return;
            TemperatureSlider.Value = _chatService.CurrentTemperature;
            TemperatureValueLabel.Text = $"Current: {_chatService.CurrentTemperature:F2}";
            ConversationsListView.SelectedItem = _chatService.CurrentConversation;
            UpdateControlStates();
        }

        private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_chatService == null) return;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(EasyChatService.CurrentEngineState):
                        UpdateControlStates();
                        break;
                    case nameof(EasyChatService.CurrentConversation):
                        if (ConversationsListView.SelectedItem != _chatService.CurrentConversation)
                        {
                            ConversationsListView.SelectedItem = _chatService.CurrentConversation;
                        }
                        break;
                    case nameof(EasyChatService.CurrentTemperature):
                        TemperatureSlider.Value = _chatService.CurrentTemperature;
                        TemperatureValueLabel.Text = $"Current: {_chatService.CurrentTemperature:F2}";
                        break;
                }
            });
        }

        private void UpdateControlStates()
        {
            if (_chatService == null) return;
            NewChatButton.IsEnabled = true;
            bool isBusy = _chatService.CurrentEngineState == EngineState.BUSY;
            ModelsButton.IsEnabled = !isBusy;
            SettingsButton.IsEnabled = !isBusy;
            ConversationsListView.IsEnabled = !isBusy;
        }

        private async void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService == null) return;
            _chatService.StartNewConversation();
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//ChatContentPage");
                Shell.Current.FlyoutIsPresented = false;
            }
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_chatService == null || e.CurrentSelection.FirstOrDefault() is not ChatHistory selected) return;

            // If user re-selects the same conversation, do nothing.
            if (_chatService.CurrentConversation?.Id == selected.Id)
            {
                if (Shell.Current != null) Shell.Current.FlyoutIsPresented = false;
                return;
            }

            // Always allow selection and viewing. The ChatContentPage will handle the UI state.
            await _chatService.SelectConversationAsync(selected);
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync("//ChatContentPage");
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
            if (_chatService == null) return;
            if (!_chatService.IsIdle) return;

            float newTemp = (float)e.NewValue;
            TemperatureValueLabel.Text = $"Current: {newTemp:F2}";

            _chatService.UpdateSamplingParams(newTemp, _chatService.CurrentMinP);
        }

        private async void ModelsButton_Clicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(ModelsPage));
                Shell.Current.FlyoutIsPresented = false;
            }
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            if (Shell.Current != null)
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
                Shell.Current.FlyoutIsPresented = false;
            }
        }
    }

    public static class VisualElementExtensions
    {
        public static Page? GetParentPage(this VisualElement element)
        {
            Element? parent = element.Parent;
            while (parent != null && parent is not Page)
            {
                parent = parent.Parent;
            }
            return parent as Page;
        }
    }
}