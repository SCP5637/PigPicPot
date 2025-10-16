using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.Messaging;
using PigPicPot.Helpers;

namespace PigPicPot.ViewModels
{
    /// <summary>
    /// 主窗口视图模型，负责管理主界面的数据和逻辑
    /// Main window view model, responsible for managing data and logic of the main interface
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ITagProvider _tagProvider;
        private readonly IThumbnailService _thumbnailService;
        private readonly IMessenger _messenger;
        private readonly Dispatcher _dispatcher;

        private ReadOnlyCollection<ImageItem> _allImageItems = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());
        /// <summary>
        /// 所有图片项的只读集合
        /// Read-only collection of all image items
        /// </summary>
        public ReadOnlyCollection<ImageItem> AllImages => _allImageItems;
        private string _searchText = "";
        private readonly List<TagNode> _selectedTags = new List<TagNode>();

        /// <summary>
        /// 一级标签集合
        /// Level 1 tags collection
        /// </summary>
        public ObservableCollection<TagNode> Level1Tags { get; } = new ObservableCollection<TagNode>();
        
        /// <summary>
        /// 二级标签集合
        /// Level 2 tags collection
        /// </summary>
        public ObservableCollection<TagNode> Level2Tags { get; } = new ObservableCollection<TagNode>();
        
        /// <summary>
        /// 三级标签集合
        /// Level 3 tags collection
        /// </summary>
        public ObservableCollection<TagNode> Level3Tags { get; } = new ObservableCollection<TagNode>();
        
        /// <summary>
        /// 收藏标签集合
        /// Favorite tags collection
        /// </summary>
        public ObservableCollection<Favorite> FavoriteTags { get; set; } = new ObservableCollection<Favorite>();
        
        /// <summary>
        /// 筛选后的图片项集合
        /// Filtered image items collection
        /// </summary>
        public ObservableCollection<ImageItem> FilteredItems { get; } = new ObservableCollection<ImageItem>();

        /// <summary>
        /// 当前激活的收藏筛选器
        /// Currently active favorite filter
        /// </summary>
        public Favorite? ActiveFavoriteFilter { get; private set; }

        /// <summary>
        /// 搜索文本属性
        /// Search text property
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                ApplyFilters();
            }
        }

        // Statistics properties
        /// <summary>
        /// 总图片数
        /// Total number of images
        /// </summary>
        public int TotalImages { get; private set; }
        
        /// <summary>
        /// 成功加载的图片数
        /// Number of successfully loaded images
        /// </summary>
        public int SuccessfulImages { get; private set; }
        
        /// <summary>
        /// 加载失败的图片数
        /// Number of failed images
        /// </summary>
        public int FailedImages { get; private set; }
        
        /// <summary>
        /// 动态图片数
        /// Number of animated images
        /// </summary>
        public int AnimatedImages { get; private set; }
        
        /// <summary>
        /// 静态图片数
        /// Number of static images
        /// </summary>
        public int StaticImages { get; private set; }

        /// <summary>
        /// 总项目数
        /// Total items count
        /// </summary>
        public int TotalItemsCount => _allImageItems?.Count ?? 0;
        
        /// <summary>
        /// 筛选后的项目数
        /// Filtered items count
        /// </summary>
        public int FilteredItemsCount => FilteredItems.Count;

        /// <summary>
        /// 摘要文本
        /// Summary text
        /// </summary>
        public string SummaryText => $"当前页面: {FilteredItemsCount} 张图片 / 总共: {TotalItemsCount} 张图片";
        
        /// <summary>
        /// 加载摘要文本
        /// Load summary text
        /// </summary>
        public string LoadSummaryText => $"本次启动加载了 {TotalImages} 张图片，成功 {SuccessfulImages} 张，失败 {FailedImages} 张。其中动态图片 {AnimatedImages} 张，静态图片 {StaticImages} 张。";

        /// <summary>
        /// 选择标签命令
        /// Select tag command
        /// </summary>
        public ICommand SelectTagCommand { get; }
        
        /// <summary>
        /// 复制图片命令
        /// Copy image command
        /// </summary>
        public ICommand CopyImageCommand { get; }

        /// <summary>
        /// 私有构造函数，初始化视图模型
        /// Private constructor to initialize the view model
        /// </summary>
        /// <param name="tagProvider">标签提供者</param>
        /// <param name="thumbnailService">缩略图服务</param>
        /// <param name="messenger">消息传递器</param>
        /// <param name="dispatcher">调度器</param>
        private MainViewModel(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher? dispatcher = null)
        {
            _tagProvider = tagProvider;
            _thumbnailService = thumbnailService;
            _messenger = messenger;
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            SelectTagCommand = new RelayCommand<TagNode>(SelectTag);
            CopyImageCommand = new RelayCommand<ImageItem>(CopyImage);
        }

        /// <summary>
        /// 异步创建并初始化MainViewModel实例
        /// Asynchronously create and initialize MainViewModel instance
        /// </summary>
        /// <param name="tagProvider">标签提供者</param>
        /// <param name="thumbnailService">缩略图服务</param>
        /// <param name="messenger">消息传递器</param>
        /// <param name="dispatcher">调度器</param>
        /// <returns>初始化完成的MainViewModel实例</returns>
        public static async Task<MainViewModel> CreateAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher dispatcher)
        {
            var viewModel = new MainViewModel(tagProvider, thumbnailService, messenger, dispatcher);
            await viewModel.InitializeAsync();
            return viewModel;
        }

        /// <summary>
        /// 异步初始化方法
        /// Asynchronous initialization method
        /// </summary>
        private async Task InitializeAsync()
        {
            await _tagProvider.LoadAsync();

            _allImageItems = _tagProvider.AllImageItems;
            CalculateStatistics();

            _dispatcher.Invoke(() =>
            {
                Level1Tags.Clear();
                foreach (var tag in _tagProvider.RootTags)
                {
                    Level1Tags.Add(tag);
                }

                OnPropertyChanged(nameof(TotalItemsCount));
                OnPropertyChanged(nameof(SummaryText));
                OnPropertyChanged(nameof(LoadSummaryText));

                FilteredItems.Clear();
                OnPropertyChanged(nameof(FilteredItemsCount));

                _messenger.Send(new ShowNotificationMessage($"Loaded {TotalItemsCount} images"));
            });

            _thumbnailService.Prioritize(System.Linq.Enumerable.Empty<ImageItem>(), _allImageItems);
        }

        /// <summary>
        /// 计算统计数据
        /// Calculate statistics data
        /// </summary>
        private void CalculateStatistics()
        {
            if (_allImageItems == null) return;

            TotalImages = _allImageItems.Count;
            SuccessfulImages = _allImageItems.Count;
            FailedImages = 0;
            AnimatedImages = _allImageItems.Count(item => item.IsAnimated);
            StaticImages = _allImageItems.Count(item => !item.IsAnimated);
            OnPropertyChanged(nameof(LoadSummaryText));
        }

        /// <summary>
        /// 清除标签选择
        /// Clear tag selections
        /// </summary>
        public void ClearTagSelections()
        {
            ClearAllTagSelections(Level1Tags);
            _selectedTags.Clear();
            UpdateDisplayedTags();
        }

        /// <summary>
        /// 选择标签
        /// Select tag
        /// </summary>
        /// <param name="tag">要选择的标签节点</param>
        private void SelectTag(TagNode tag)
        {
            var newSelection = tag.IsSelected ? tag.Parent : tag;

            ClearAllTagSelections(Level1Tags);
            _selectedTags.Clear();

            if (newSelection != null)
            {
                newSelection.IsSelected = true;
                _selectedTags.Add(newSelection);
                var parent = newSelection.Parent;
                while (parent != null)
                {
                    parent.IsSelected = true;
                    _selectedTags.Add(parent);
                    parent = parent.Parent;
                }
            }

            UpdateDisplayedTags();
            ApplyFilters();
        }

        private void UpdateDisplayedTags()
        {
            Level2Tags.Clear();
            Level3Tags.Clear();

            var selectedL1 = _selectedTags.FirstOrDefault(t => t.Level == 1);
            if (selectedL1 != null)
            {
                foreach (var child in selectedL1.Children)
                {
                    Level2Tags.Add(child);
                }
            }

            var selectedL2 = _selectedTags.FirstOrDefault(t => t.Level == 2);
            if (selectedL2 != null)
            {
                foreach (var child in selectedL2.Children)
                {
                    Level3Tags.Add(child);
                }
            }
        }

        private void ClearAllTagSelections(IEnumerable<TagNode> tags)
        {
            foreach (var tag in tags)
            {
                tag.IsSelected = false;
                ClearAllTagSelections(tag.Children);
            }
        }

        public void ApplyFilters(Favorite? favorite = null)
        {
            ActiveFavoriteFilter = favorite;
            if (_allImageItems == null) return;

            IEnumerable<ImageItem> query;

            if (favorite != null)
            {
                var favoriteImagePaths = new HashSet<string>(favorite.Images.Select(img => img.FilePath));
                query = _allImageItems.Where(item => favoriteImagePaths.Contains(item.FilePath));
            }
            else
            {
                query = _allImageItems;

                if (_selectedTags.Count > 0)
                {
                    query = query.Where(item => _selectedTags.All(tag => IsItemInTag(item, tag)));
                }

                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    if (_selectedTags.Count == 0)
                    {
                        query = Enumerable.Empty<ImageItem>();
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerms = SearchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                query = query.Where(item =>
                    searchTerms.All(term =>
                        (item.FileName != null && item.FileName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.BaseChineseName != null && item.BaseChineseName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.Tags != null && item.Tags.Any(tag => tag != null && tag.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))
                    )
                );
            }

            var result = query.ToList();

            _dispatcher.Invoke(() =>
            {
                FilteredItems.Clear();
                foreach (var item in result)
                {
                    FilteredItems.Add(item);
                }

                OnPropertyChanged(nameof(FilteredItemsCount));
                OnPropertyChanged(nameof(SummaryText));

                _thumbnailService.Prioritize(result, _allImageItems);
            });
        }

        private bool IsItemInTag(ImageItem item, TagNode tag)
        {
            if (tag.IsSeriesTag)
            {
                if (tag.DirectoryName == "其他")
                {
                    var seriesTags = tag.Parent?.Children.Where(t => t.IsSeriesTag && t.DirectoryName != "其他").Select(t => t.DirectoryName);
                    if (seriesTags == null || string.IsNullOrEmpty(item.BaseChineseName)) return false;
                    return !seriesTags.Contains(item.BaseChineseName);
                }
                return item.BaseChineseName == tag.DirectoryName;
            }
            else
            {
                return item.Tags.Contains(tag.DirectoryName);
            }
        }

        private void CopyImage(ImageItem item)
        {
            try
            {
                if (item.IsAnimated)
                {
                    ClipboardHelper.SetAnimatedGif(item.FilePath);
                }
                else
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new System.Uri(item.FilePath));
                    System.Windows.Clipboard.SetImage(bitmap);
                }

                _messenger.Send(new ShowNotificationMessage("Copied to clipboard!"));
            }
            catch (System.Exception ex)
            {
                _messenger.Send(new ShowNotificationMessage($"Copy failed: {ex.Message}"));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
