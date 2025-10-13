using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ITagProvider _tagProvider;
    private readonly IThumbnailService _thumbnailService;
    private readonly IMessenger _messenger;
    private readonly Dispatcher _dispatcher;

    private ReadOnlyCollection<ImageItem> _allImageItems = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());
    private string _searchText = "";
    private readonly List<TagNode> _selectedTags = new List<TagNode>();

    public ObservableCollection<TagNode> Level1Tags { get; } = new ObservableCollection<TagNode>();
    public ObservableCollection<TagNode> Level2Tags { get; } = new ObservableCollection<TagNode>();
    public ObservableCollection<TagNode> Level3Tags { get; } = new ObservableCollection<TagNode>();
    public ObservableCollection<ImageItem> FilteredItems { get; } = new ObservableCollection<ImageItem>();
    
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

    // 统计信息属性
    private int _totalImages;
    private int _successfulImages;
    private int _failedImages;
    private int _animatedImages;
    private int _staticImages;

    public int TotalImages
    {
        get => _totalImages;
        set
        {
            _totalImages = value;
            OnPropertyChanged(nameof(TotalImages));
        }
    }

    public int SuccessfulImages
    {
        get => _successfulImages;
        set
        {
            _successfulImages = value;
            OnPropertyChanged(nameof(SuccessfulImages));
        }
    }

    public int FailedImages
    {
        get => _failedImages;
        set
        {
            _failedImages = value;
            OnPropertyChanged(nameof(FailedImages));
        }
    }

    public int AnimatedImages
    {
        get => _animatedImages;
        set
        {
            _animatedImages = value;
            OnPropertyChanged(nameof(AnimatedImages));
        }
    }

    public int StaticImages
    {
        get => _staticImages;
        set
        {
            _staticImages = value;
            OnPropertyChanged(nameof(StaticImages));
        }
    }

    public int TotalItemsCount => _allImageItems?.Count ?? 0;
    public int FilteredItemsCount => FilteredItems.Count;

    public string SummaryText => $"当前页面: {FilteredItemsCount} 张图片 / 总共: {TotalItemsCount} 张图片";
    public string LoadSummaryText => $"本次启动加载了 {TotalImages} 张图片，成功 {SuccessfulImages} 张，失败 {FailedImages} 张。其中动态图片 {AnimatedImages} 张，静态图片 {StaticImages} 张。";

    public ICommand SelectTagCommand { get; }
    public ICommand CopyImageCommand { get; }

    public MainViewModel(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher? dispatcher = null)
    {
        _tagProvider = tagProvider;
        _thumbnailService = thumbnailService;
        _messenger = messenger;
        _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

        SelectTagCommand = new RelayCommand<TagNode>(SelectTag);
        CopyImageCommand = new RelayCommand<ImageItem>(CopyImage);

        // Non-blocking async loading
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _tagProvider.LoadAsync();

        // 更新UI状态
        _allImageItems = _tagProvider.AllImageItems;
        
        // 计算统计信息
        CalculateStatistics();
        
        // 更新标签
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
            
            // 默认不显示任何图片，等待用户选择标签
            FilteredItems.Clear();
            OnPropertyChanged(nameof(FilteredItemsCount));
            
            // 发送消息通知加载完成
            _messenger.Send(new ShowNotificationMessage($"Loaded {TotalItemsCount} images"));
        });
    }

    private void CalculateStatistics()
    {
        if (_allImageItems == null) return;
        
        TotalImages = _allImageItems.Count;
        SuccessfulImages = _allImageItems.Count; // 假设所有图片都成功加载
        FailedImages = 0; // 需要根据实际加载情况计算
        AnimatedImages = _allImageItems.Count(item => item.IsAnimated);
        StaticImages = _allImageItems.Count(item => !item.IsAnimated);
    }

    private void SelectTag(TagNode tag)
    {
        var newSelection = tag;
        if (tag.IsSelected)
        {
            newSelection = tag.Parent;
        }

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
            foreach(var child in selectedL1.Children)
            {
                Level2Tags.Add(child);
            }
        }

        var selectedL2 = _selectedTags.FirstOrDefault(t => t.Level == 2);
        if (selectedL2 != null)
        {
            foreach(var child in selectedL2.Children)
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


    private void ApplyFilters()
    {
        if (_allImageItems == null) return;

        // Start with all images.
        IEnumerable<ImageItem> query = _allImageItems;

        // Apply tag filter if any tags are selected.
        if (_selectedTags.Count > 0)
        {
            query = query.Where(item => _selectedTags.All(tag => IsItemInTag(item, tag)));
        }

        // Apply text search filter if there is search text.
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchTerms = SearchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            query = query.Where(item =>
                searchTerms.All(term =>
                    (item.FileName != null && item.FileName.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (item.BaseChineseName != null && item.BaseChineseName.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (item.Tags != null && item.Tags.Any(tag => tag != null && tag.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0))
                )
            );
        }

        // If no filters are active, show nothing.
        if (_selectedTags.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
        {
            query = Enumerable.Empty<ImageItem>();
        }

        var result = query.ToList();

        // Update filtered results
        _dispatcher.Invoke(() =>
        {
            FilteredItems.Clear();
            foreach (var item in result)
            {
                FilteredItems.Add(item);
            }
            
            OnPropertyChanged(nameof(FilteredItemsCount));
            OnPropertyChanged(nameof(SummaryText));
        });
    }

    private bool IsItemInTag(ImageItem item, TagNode tag)
    {
        if (tag.IsSeriesTag)
        {
            if (tag.DirectoryName == "其他")
            {
                // Item is in the parent directory (checked by the .All() in ApplyFilters)
                // Now, check if its BaseChineseName does NOT match any sibling series tags.
                var seriesTags = tag.Parent?.Children.Where(t => t.IsSeriesTag && t.DirectoryName != "其他").Select(t => t.DirectoryName);
                if (seriesTags == null || string.IsNullOrEmpty(item.BaseChineseName)) return false;
                return !seriesTags.Contains(item.BaseChineseName);
            }
            return item.BaseChineseName == tag.DirectoryName;
        }
        else
        {
            // This is a directory tag, check if the item's path contains this directory name
            // This is a simplification. A better check would be to reconstruct the path.
            // For now, this relies on directory names being unique enough.
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