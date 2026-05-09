using System.ComponentModel;
using System.Windows.Media;

namespace ImageEnhancementWpf
{
    public class MethodItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Id { get; set; } = "";
        public string Category { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Formula { get; set; } = "";
        public string Principle { get; set; } = "";
        public bool IsAvailable { get; set; } = true;
        public Brush CategoryAccentBrush { get; set; } = new SolidColorBrush(Color.FromRgb(99, 102, 241));

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
