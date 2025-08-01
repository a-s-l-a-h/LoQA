using LoQA.Models;
using LoQA.Services;
using System.ComponentModel;

namespace LoQA.Views
{
    public partial class SidebarView : ContentView
    {
        private EasyChatService _chatService;
        private DatabaseService _databaseService;

        public SidebarView()
        {
            InitializeComponent();
            // We'll retrieve the DatabaseService once the view is attached to the visual tree.
            this.HandlerChanged += (s, e) => {
                if (Handler != null)
                {
                    _databaseService = Handler.MauiContext.Services.GetService<DatabaseService>();
                }
            };
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            // The BindingContext is set to the EasyChatService by the parent HomePage.
            if (BindingContext is EasyChatService service)
            {
                if (_chatService != null)
                {
                    _chatService.PropertyChanged -= OnServicePropertyChanged;
                }

                _chatService = service;
                _chatService.PropertyChanged += OnServicePropertyChanged;

                // Sync initial state from the service
                var currentParams = _chatService.GetCurrentSamplingParams();
                TemperatureSlider.Value = currentParams.temperature;
                TemperatureValueLabel.Text = $"Current: {currentParams.temperature:F2}";
                ConversationsListView.SelectedItem = _chatService.CurrentConversation;
                UpdateGeneratingState(_chatService.IsGenerating);
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
                        if (ConversationsListView.SelectedItem != _chatService.CurrentConversation)
                        {
                            ConversationsListView.SelectedItem = _chatService.CurrentConversation;
                        }
                        break;
                }
            });
        }

        private void UpdateGeneratingState(bool isGenerating)
        {
            NewChatButton.IsEnabled = !isGenerating;
            ConversationsListView.IsEnabled = !isGenerating;
            SettingsButton.IsEnabled = !isGenerating;
            TemperatureSlider.IsEnabled = !isGenerating;
        }

        private void NewChatButton_Clicked(object sender, EventArgs e)
        {
            if (_chatService.IsGenerating) return;
            ConversationsListView.SelectedItem = null;
            _chatService.StartNewConversation();
        }

        private async void ConversationsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not ChatHistory selected) return;
            if (_chatService.CurrentConversation?.Id == selected.Id) return;

            await _chatService.LoadConversationAsync(selected);
        }

        private async void DeleteConversation_Invoked(object sender, EventArgs e)
        {
            if ((sender as SwipeItem)?.CommandParameter is not ChatHistory convToDelete) return;

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
            if (!_chatService.IsInitialized) return;
            var currentParams = _chatService.GetCurrentSamplingParams();
            currentParams.temperature = (float)e.NewValue;
            _chatService.UpdateSamplingParams(currentParams);
        }

        private async void SettingsButton_Clicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("///SettingsPage");
        }
    }

    public static class VisualElementExtensions
    {
        public static Page GetParentPage(this VisualElement element)
        {
            Element parent = element.Parent;
            while (parent != null && !(parent is Page))
            {
                parent = parent.Parent;
            }
            return parent as Page;
        }
    }
}