using LoQA.Models;
using LoQA.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;

namespace LoQA.Views
{
    public partial class ModelsPage : ContentPage
    {
        private readonly DatabaseService _databaseService;
        private readonly EasyChatService _chatService;
        public ObservableCollection<LlmModel> Models { get; } = new();

        public ModelsPage(DatabaseService databaseService, EasyChatService chatService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            _chatService = chatService;
            ModelsListView.ItemsSource = Models;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _chatService.PropertyChanged += OnChatServicePropertyChanged;
            Task.Run(LoadModelsAsync);
            UpdateStatusLabel();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _chatService.PropertyChanged -= OnChatServicePropertyChanged;
        }

        private void OnChatServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(EasyChatService.IsInitialized) or nameof(EasyChatService.LoadedModel))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await LoadModelsAsync();
                    UpdateStatusLabel();
                });
            }
        }

        private async Task LoadModelsAsync()
        {
            var modelsFromDb = await _databaseService.GetModelsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Models.Clear();
                foreach (var model in modelsFromDb)
                {
                    model.IsActive = _chatService.LoadedModel?.Id == model.Id;
                    Models.Add(model);
                }
            });
        }

        private void UpdateStatusLabel()
        {
            if (_chatService.IsInitialized && _chatService.LoadedModel != null)
            {
                StatusLabel.Text = $"Loaded: {_chatService.LoadedModel.Name}";
            }
            else
            {
                StatusLabel.Text = "No model is currently loaded.";
            }
        }

        private async void LoadButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not LlmModel model) return;

            model.IsLoading = true;
            model.LoadingError = null;

            try
            {
                await _chatService.LoadModelAsync(model);
            }
            catch (Exception ex)
            {
                model.LoadingError = $"Failed to load: {ex.Message}";
            }
            finally
            {
                model.IsLoading = false;

                foreach (var m in Models)
                {
                    m.IsActive = _chatService.LoadedModel?.Id == m.Id;
                }
                UpdateStatusLabel();
            }
        }

        private async void AddNewModel_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".gguf" } },
                        { DevicePlatform.Android, new[] { "application/octet-stream", "*/*" } },
                        { DevicePlatform.iOS, new[] { "public.data" } },
                        { DevicePlatform.MacCatalyst, new[] { "gguf" } },
                    });

                var pickResult = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select a GGUF Model File",
                    FileTypes = customFileType
                });

                if (pickResult == null) return;

                StatusLabel.Text = $"Copying {pickResult.FileName}... Please wait.";

                var newModel = await Task.Run(async () =>
                {
                    var modelsDir = Path.Combine(FileSystem.AppDataDirectory, "models");
                    Directory.CreateDirectory(modelsDir);
                    var destinationPath = Path.Combine(modelsDir, pickResult.FileName);

                    using (var sourceStream = await pickResult.OpenReadAsync())
                    using (var destinationStream = File.Create(destinationPath))
                    {
                        await sourceStream.CopyToAsync(destinationStream);
                    }

                    return new LlmModel
                    {
                        Name = Path.GetFileNameWithoutExtension(pickResult.FileName),
                        FilePath = destinationPath,
                        SourceType = ModelSourceType.Local,
                        IsActive = false
                    };
                });

                await _databaseService.SaveModelAsync(newModel);
                Models.Add(newModel);

                StatusLabel.Text = "Model added successfully!";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error adding new model: {ex.Message}");
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Error", $"Failed to add model: {ex.Message}", "OK");
                }
            }
            finally
            {
                UpdateStatusLabel();
            }
        }

        private async void DeleteButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not LlmModel model) return;

            bool confirm = false;
            if (Shell.Current != null)
            {
                confirm = await Shell.Current.DisplayAlert("Delete Model?", $"Are you sure you want to delete '{model.Name}'? The file will also be removed.", "Delete", "Cancel");
            }

            if (!confirm) return;

            try
            {
                if (_chatService.LoadedModel?.Id == model.Id)
                {
                    await _chatService.UnloadModelAsync();
                }

                if (File.Exists(model.FilePath))
                {
                    File.Delete(model.FilePath);
                }

                await _databaseService.DeleteModelAsync(model.Id);
                Models.Remove(model);
            }
            catch (Exception ex)
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Error", $"Failed to delete model: {ex.Message}", "OK");
                }
            }
        }

        private async void UnloadButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not LlmModel model) return;

            if (_chatService.LoadedModel?.Id != model.Id)
            {
                await DisplayAlert("Info", "This model is not currently loaded.", "OK");
                return;
            }

            StatusLabel.Text = "Unloading model...";
            try
            {
                await _chatService.UnloadModelAsync();
            }
            catch (Exception ex)
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert("Error", $"Failed to unload model: {ex.Message}", "OK");
                }
                await LoadModelsAsync();
                UpdateStatusLabel();
            }
        }

        // =========================================================================
        // === ACTION: REMOVED THE BackButton_Clicked EVENT HANDLER              ===
        // =========================================================================
        // This method is no longer needed because the button was removed from the XAML.
    }
}