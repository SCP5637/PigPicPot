using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

public class MiniViewModel : INotifyPropertyChanged
{
    private readonly ITagProvider _tagProvider;
    private readonly IThumbnailService _thumbnailService;
    private readonly ISettingsService _settingsService;
    private readonly IMessenger _messenger;
    private readonly Dispatcher _dispatcher;

    public IThumbnailService ThumbnailService => _thumbnailService;
    public IMessenger Messenger => _messenger;

    private ReadOnlyCollection<ImageItem> _allImageItems = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());
    private string _searchText = "";
    private readonly List<TagNode> _selectedTags = new List<TagNode>();
    private bool _isPinned;

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
    
    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (_isPinned != value)
            {
                _isPinned = value;
                _ = _settingsService.SavePinState(value);
                OnPropertyChanged(nameof(IsPinned));
            }
        }
    }

    public int TotalItemsCount => _allImageItems?.Count ?? 0;
    public int FilteredItemsCount => FilteredItems.Count;

    public ICommand SelectTagCommand { get; }
    public ICommand CopyImageCommand { get; }

    public MiniViewModel(ITagProvider tagProvider, IThumbnailService thumbnailService, ISettingsService settingsService, IMessenger messenger, Dispatcher? dispatcher = null)
    {
        _tagProvider = tagProvider;
        _thumbnailService = thumbnailService;
        _settingsService = settingsService;
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

        // Update UI state
        _allImageItems = _tagProvider.AllImageItems;
        
        // Update tags
        _dispatcher.Invoke(() =>
        {
            Level1Tags.Clear();
            foreach (var tag in _tagProvider.RootTags)
            {
                Level1Tags.Add(tag);
            }
            
            OnPropertyChanged(nameof(TotalItemsCount));
            
            // Default to showing no images, wait for user to select a tag
            FilteredItems.Clear();
            OnPropertyChanged(nameof(FilteredItemsCount));
        });

        // Load pin state
        var pinState = await _settingsService.LoadPinState();
        _dispatcher.Invoke(() => IsPinned = pinState);
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
                System.Windows.Clipboard.SetImage(new BitmapImage(new System.Uri(item.FilePath)));
            }

            _messenger.Send(new ShowNotificationMessage("Copied to clipboard!"));

            if (!IsPinned)
            {
                _messenger.Send(new CloseMiniWindowMessage());
            }
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