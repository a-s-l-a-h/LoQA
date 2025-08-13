using LoQA.Models;
using LoQA.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
// FIX: Added alias to resolve ambiguity between UI Switch and Diagnostics Switch
using Switch = Microsoft.Maui.Controls.Switch;

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

            _chatService.PropertyChanged += OnChatServicePropertyChanged;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadModelsAsync();
            UpdateStatusLabel();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private void OnChatServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(EasyChatService.CurrentEngineState) or nameof(EasyChatService.LoadedModel))
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

            foreach (var dbModel in modelsFromDb)
            {
                var existingModel = Models.FirstOrDefault(m => m.Id == dbModel.Id);
                if (existingModel != null)
                {
                    existingModel.Name = dbModel.Name;
                    existingModel.FilePath = dbModel.FilePath;
                    existingModel.CustomCtx = dbModel.CustomCtx;
                    existingModel.CustomGpuLayers = dbModel.CustomGpuLayers;
                    existingModel.CustomTemperature = dbModel.CustomTemperature;
                    existingModel.CustomMinP = dbModel.CustomMinP;
                    existingModel.CustomChatTemplate = dbModel.CustomChatTemplate;
                    existingModel.IsDefault = dbModel.IsDefault;
                    existingModel.IsActive = _chatService.LoadedModel?.Id == dbModel.Id;
                }
                else
                {
                    dbModel.IsActive = _chatService.LoadedModel?.Id == dbModel.Id;
                    Models.Add(dbModel);
                }
            }
            var modelsToRemove = Models.Where(m => !modelsFromDb.Any(db => db.Id == m.Id)).ToList();
            foreach (var modelToRemove in modelsToRemove)
            {
                Models.Remove(modelToRemove);
            }
        }

        private void UpdateStatusLabel()
        {
            if (_chatService.LoadedModel != null && _chatService.CurrentEngineState != EngineState.UNINITIALIZED)
            {
                StatusLabel.Text = $"Loaded: {_chatService.LoadedModel.Name} (State: {_chatService.CurrentEngineState})";
            }
            else
            {
                StatusLabel.Text = $"No model loaded. (State: {_chatService.CurrentEngineState})";
            }
            if (_chatService.CurrentEngineState == EngineState.IN_ERROR)
            {
                StatusLabel.Text += $"\nError: {_chatService.LastErrorMessage}";
                StatusLabel.TextColor = Colors.Red;
            }
            else
            {
                var secondaryTextColor = Application.Current?.Resources["SecondaryTextColor"];
                if (secondaryTextColor is Color color)
                {
                    StatusLabel.TextColor = color;
                }
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
                if (_chatService.CurrentEngineState == EngineState.IN_ERROR)
                {
                    throw new Exception(_chatService.LastErrorMessage);
                }
            }
            catch (Exception ex)
            {
                model.LoadingError = $"Failed to load: {ex.Message}";
            }
            finally
            {
                model.IsLoading = false;
                await LoadModelsAsync();
                UpdateStatusLabel();
            }
        }

        private async void UnloadButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not LlmModel model) return;
            if (_chatService.LoadedModel?.Id != model.Id) return;

            StatusLabel.Text = "Unloading model...";
            await _chatService.UnloadModelAsync();
        }

        private async void DeleteButton_Clicked(object? sender, EventArgs e)
        {
            if (sender is not Button button || button.CommandParameter is not LlmModel model) return;
            if (Shell.Current == null) return;
            bool confirm = await Shell.Current.DisplayAlert("Delete Model?", $"Are you sure you want to delete '{model.Name}'? The file will also be removed.", "Delete", "Cancel");
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
                await Shell.Current.DisplayAlert("Error", $"Failed to delete model: {ex.Message}", "OK");
            }
        }

        private async void DefaultModelSwitch_Toggled(object sender, ToggledEventArgs e)
        {
            if (sender is not Switch { BindingContext: LlmModel model }) return;

            if (e.Value)
            {
                foreach (var otherModel in Models.Where(m => m.Id != model.Id))
                {
                    otherModel.IsDefault = false;
                }
            }

            await _databaseService.SaveModelAsync(model);
        }

        #region Settings Panel Methods

        private void ToggleSettings_Clicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: LlmModel model })
            {
                model.IsExpanded = !model.IsExpanded;
            }
        }

        private async void SaveSettings_Clicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: LlmModel model } && Shell.Current != null)
            {
                await _databaseService.SaveModelAsync(model);
                await Shell.Current.DisplayAlert("Saved", $"Custom settings for '{model.Name}' have been saved.", "OK");
            }
        }

        private async void ResetSettings_Clicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: LlmModel model } && Shell.Current != null)
            {
                bool confirm = await Shell.Current.DisplayAlert("Reset Settings?", "This will clear all custom parameters for this model. Are you sure?", "Reset", "Cancel");
                if (!confirm) return;

                model.CustomCtx = null;
                model.CustomGpuLayers = null;
                model.CustomTemperature = null;
                model.CustomMinP = null;
                model.CustomChatTemplate = null;

                await _databaseService.SaveModelAsync(model);
                await Shell.Current.DisplayAlert("Reset Complete", $"Settings for '{model.Name}' have been reset to default.", "OK");
            }
        }

        #endregion

        private async void AddNewModel_Clicked(object? sender, EventArgs e)
        {
            try
            {
                var customFileType = new FilePickerFileType(
                    new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".gguf" } },
                        { DevicePlatform.Android, new[] { "application/octet-stream" } },
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
    }
}