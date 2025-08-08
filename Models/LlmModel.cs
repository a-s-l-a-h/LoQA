// C:\MYWORLD\Projects\LoQA\LoQA\Models\LlmModel.cs
using SQLite;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoQA.Models
{
    public enum ModelSourceType { Local, Internet }

    [Table("llm_models")]
    public class LlmModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public ModelSourceType SourceType { get; set; } = ModelSourceType.Local;

        public int? CustomCtx { get; set; }
        public int? CustomGpuLayers { get; set; }
        public float? CustomTemperature { get; set; }
        public float? CustomMinP { get; set; }
        public string? CustomChatTemplate { get; set; }

        private bool _isDefault;
        public bool IsDefault
        {
            get => _isDefault;
            set => SetField(ref _isDefault, value);
        }

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        private bool _isExpanded;
        [Ignore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }

        private bool _isLoading;
        [Ignore]
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private string? _loadingError;
        [Ignore]
        public string? LoadingError
        {
            get => _loadingError;
            set
            {
                if (SetField(ref _loadingError, value))
                {
                    OnPropertyChanged(nameof(HasLoadingError));
                }
            }
        }

        [Ignore]
        public bool HasLoadingError => !string.IsNullOrEmpty(LoadingError);

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