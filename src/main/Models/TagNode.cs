namespace PigPicPot.Models
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;

    public class TagNode : INotifyPropertyChanged
    {
        public string DirectoryName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public int Level { get; set; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        public bool IsSeriesTag { get; set; }
        public ObservableCollection<TagNode> Children { get; set; } = new ObservableCollection<TagNode>();
        public TagNode? Parent { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
