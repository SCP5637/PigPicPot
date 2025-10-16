using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PigPicPot.Models
{
    /// <summary>
    /// 收藏图像类，表示收藏夹中的单个图像
    /// Favorite image class, represents a single image in favorites
    /// </summary>
    public class FavoriteImage
    {
        /// <summary>
        /// 文件路径
        /// File path
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件名
        /// File name
        /// </summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// 文件哈希值
        /// File hash
        /// </summary>
        public string Hash { get; set; } = string.Empty;
    }

    /// <summary>
    /// 收藏夹类，表示一个收藏夹
    /// Favorite class, represents a favorites folder
    /// </summary>
    public class Favorite : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        
        /// <summary>
        /// 收藏夹名称
        /// Favorite name
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        /// <summary>
        /// 是否可删除
        /// Whether deletable
        /// </summary>
        public bool IsDeletable { get; set; }
        
        /// <summary>
        /// 收藏的图像列表
        /// List of favorite images
        /// </summary>
        public List<FavoriteImage> Images { get; set; } = new List<FavoriteImage>();

        private bool _isSelected;
        
        /// <summary>
        /// 是否被选中
        /// Whether selected
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
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

    public class FavoritesData
    {
        public List<Favorite> Favorites { get; set; } = new List<Favorite>();
    }
}
