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

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}