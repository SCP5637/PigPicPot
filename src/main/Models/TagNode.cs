namespace PigPicPot.Models
{
    using System.Collections.ObjectModel;
    using System.ComponentModel;

    /// <summary>
    /// 标签节点类，表示标签树中的一个节点
    /// Tag node class, represents a node in the tag tree
    /// </summary>
    public class TagNode : INotifyPropertyChanged
    {
        /// <summary>
        /// 目录名称
        /// Directory name
        /// </summary>
        public string DirectoryName { get; set; } = string.Empty;
        
        /// <summary>
        /// 显示名称
        /// Display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// 标签层级
        /// Tag level
        /// </summary>
        public int Level { get; set; }
        
        private bool _isSelected;
        
        /// <summary>
        /// 是否被选中
        /// Whether selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        
        /// <summary>
        /// 是否为系列标签
        /// Whether it's a series tag
        /// </summary>
        public bool IsSeriesTag { get; set; }
        
        /// <summary>
        /// 子节点集合
        /// Child nodes collection
        /// </summary>
        public ObservableCollection<TagNode> Children { get; set; } = new ObservableCollection<TagNode>();
        
        /// <summary>
        /// 父节点
        /// Parent node
        /// </summary>
        public TagNode? Parent { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
