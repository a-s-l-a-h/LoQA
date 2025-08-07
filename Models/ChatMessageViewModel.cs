using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Graphics;

namespace LoQA.Models
{
    public class ChatMessageViewModel : INotifyPropertyChanged
    {
        private string _content = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string Content
        {
            get => _content;
            set
            {
                if (_content != value)
                {
                    _content = value;
                    OnPropertyChanged();
                }
            }
        }

        public LayoutOptions HorizontalAlignment => Role == "user" ? LayoutOptions.End : LayoutOptions.Start;
        public Color BackgroundColor => Role == "user" ? Color.FromArgb("#405D82") : Color.FromArgb("#2C3E50");
        public Color TextColor => Colors.White;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}