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

        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        private bool _isExpanded;
        [Ignore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
        }

        // --- NEW PROPERTIES FOR UI FEEDBACK ---

        private bool _isLoading;
        [Ignore] // This state should not be saved in the database
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _loadingError;
        [Ignore] // This state should not be saved in the database
        public string? LoadingError
        {
            get => _loadingError;
            set
            {
                if (_loadingError != value)
                {
                    _loadingError = value;
                    OnPropertyChanged();
                    // Also notify that the derived property has changed
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
    }
}