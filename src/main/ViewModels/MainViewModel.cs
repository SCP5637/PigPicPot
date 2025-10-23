using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.Strings;

namespace PigPicPot.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly ITagProvider _tagProvider;
        private readonly IThumbnailService _thumbnailService;
        private readonly IMessenger _messenger;
        private readonly Dispatcher _dispatcher;

        private ReadOnlyCollection<ImageItem> _allImageItems = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());
        public ReadOnlyCollection<ImageItem> AllImages => _allImageItems;
        private string _searchText = "";
        private readonly List<TagNode> _selectedTags = new List<TagNode>();

        public ObservableCollection<TagNode> Level1Tags { get; } = new ObservableCollection<TagNode>();
        public ObservableCollection<TagNode> Level2Tags { get; } = new ObservableCollection<TagNode>();
        public ObservableCollection<TagNode> Level3Tags { get; } = new ObservableCollection<TagNode>();
        public ObservableCollection<Favorite> FavoriteTags { get; set; } = new ObservableCollection<Favorite>();
        public ObservableCollection<ImageItem> FilteredItems { get; } = new ObservableCollection<ImageItem>();

        public Favorite? ActiveFavoriteFilter { get; private set; }

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

        public int TotalImages { get; private set; }
        public int SuccessfulImages { get; private set; }
        public int FailedImages { get; private set; }
        public int AnimatedImages { get; private set; }
        public int StaticImages { get; private set; }

        public int TotalItemsCount => _allImageItems?.Count ?? 0;
        public int FilteredItemsCount => FilteredItems.Count;

        public string SummaryText => $"当前页面: {FilteredItemsCount} 张图片 / 总共: {TotalItemsCount} 张图片";
        public string LoadSummaryText => $"本次启动加载了 {TotalImages} 张图片，成功 {SuccessfulImages} 张，失败 {FailedImages} 张。其中动态图片 {AnimatedImages} 张，静态图片 {StaticImages} 张。";

        public ICommand SelectTagCommand { get; }
        public ICommand CopyImageCommand { get; }
        public IMessenger Messenger => _messenger;
        public ITagProvider TagProvider => _tagProvider;
        public IThumbnailService ThumbnailService => _thumbnailService;

        private MainViewModel(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher? dispatcher = null)
        {
            _tagProvider = tagProvider;
            _thumbnailService = thumbnailService;
            _messenger = messenger;
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            SelectTagCommand = new RelayCommand<TagNode>(SelectTag);
            CopyImageCommand = new RelayCommand<ImageItem>(CopyImage);
        }

        public static async Task<MainViewModel> CreateAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher dispatcher)
        {
            var viewModel = new MainViewModel(tagProvider, thumbnailService, messenger, dispatcher);
            await viewModel.InitializeAsync();
            return viewModel;
        }

        private async Task InitializeAsync()
        {
            var app = System.Windows.Application.Current as App;
            app?.UpdateSplashScreen("正在初始化视图模型...", 15);
            
            try
            {
                await _tagProvider.LoadAsync();
                
                _dispatcher?.BeginInvoke(() =>
                {
                    Level1Tags.Clear();
                    foreach (var tag in _tagProvider.RootTags)
                    {
                        Level1Tags.Add(tag);
                    }
                    
                    _allImageItems = _tagProvider.AllImageItems;
                    TotalImages = _allImageItems.Count;
                    SuccessfulImages = _allImageItems.Count(i => !i.IsCorrupted);
                    FailedImages = _allImageItems.Count(i => i.IsCorrupted);
                    AnimatedImages = _allImageItems.Count(i => i.IsAnimated);
                    StaticImages = TotalImages - AnimatedImages;
                    
                    OnPropertyChanged(nameof(TotalItemsCount));
                    OnPropertyChanged(nameof(SummaryText));
                    OnPropertyChanged(nameof(LoadSummaryText));
                    
                    // 设置所有图像项到缩略图服务
                    _thumbnailService.SetAllItems(_allImageItems);
                    
                    ApplyFilters();
                });
                
                app?.UpdateSplashScreen("视图模型初始化完成", 98);
                
                _messenger.Send(new ShowNotificationMessage("图像加载完成"));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing MainViewModel: {ex.Message}");
                _messenger.Send(new ShowNotificationMessage("图像加载失败"));
                throw;
            }
        }

        public void UpdateFavorites(IEnumerable<Favorite> favorites)
        {
            _dispatcher?.Invoke(() =>
            {
                FavoriteTags.Clear();
                foreach (var favorite in favorites)
                {
                    FavoriteTags.Add(favorite);
                }
            });
        }

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

        public void ClearTagSelections()
        {
            ClearAllTagSelections(Level1Tags);
            _selectedTags.Clear();
            UpdateDisplayedTags();
        }

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
                else
                {
                    // 如果没有选中标签，则不显示任何项目（设计目的）
                    query = Enumerable.Empty<ImageItem>();
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

                _messenger.Send(new ShowNotificationMessage(Strings.Resources.ImageCopiedNotification));
            }
            catch (System.Exception ex)
            {
                _messenger.Send(new ShowNotificationMessage($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}"));
            }
            finally
            {
                // 复制完成后，如果是GIF，确保释放相关资源
                if (item.IsAnimated)
                {
                    // 触发垃圾回收以释放可能的GIF资源
                    Task.Run(() => {
                        System.Threading.Thread.Sleep(100); // 等待剪贴板操作完成
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    });
                }
            }
        }

        /// <summary>
        /// 重置视图模型到初始状态
        /// </summary>
        public void ResetToInitialState()
        {
            // 重置搜索文本
            _searchText = "";
            OnPropertyChanged(nameof(SearchText));
            
            // 清理所有选择的标签
            ClearTagSelections();
            
            // 清理收藏夹选择
            foreach (var favorite in FavoriteTags)
            {
                favorite.IsSelected = false;
            }
            
            // 清理过滤后的项目
            FilteredItems.Clear();
            
            // 应用默认过滤器（无筛选状态）
            ApplyFilters();
            
            // 通知属性更改
            OnPropertyChanged(nameof(FilteredItemsCount));
            OnPropertyChanged(nameof(SummaryText));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}