using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.Messaging;
using PigPicPot.Helpers;

namespace PigPicPot.ViewModels
{
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
        public ObservableCollection<Favorite> FavoriteTags { get; } = new ObservableCollection<Favorite>();

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

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await _tagProvider.LoadAsync();
            _allImageItems = _tagProvider.AllImageItems;
            _thumbnailService.Prioritize(System.Linq.Enumerable.Empty<ImageItem>(), _allImageItems);

            _dispatcher.Invoke(() =>
            {
                Level1Tags.Clear();
                foreach (var tag in _tagProvider.RootTags)
                {
                    Level1Tags.Add(tag);
                }
                OnPropertyChanged(nameof(TotalItemsCount));
                FilteredItems.Clear();
                OnPropertyChanged(nameof(FilteredItemsCount));
            });

            var pinState = await _settingsService.LoadPinState();
            _dispatcher.Invoke(() => IsPinned = pinState);
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
            if (selectedL1 != null) foreach (var child in selectedL1.Children) Level2Tags.Add(child);

            var selectedL2 = _selectedTags.FirstOrDefault(t => t.Level == 2);
            if (selectedL2 != null) foreach (var child in selectedL2.Children) Level3Tags.Add(child);
        }

        public void ClearTagSelections()
        {
            ClearAllTagSelections(Level1Tags);
            _selectedTags.Clear();
            UpdateDisplayedTags();
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
            }

            if (_selectedTags.Any())
            {
                query = query.Where(item => _selectedTags.All(tag => IsItemInTag(item, tag)));
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchTerms = SearchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                query = query.Where(item =>
                    searchTerms.All(term =>
                        (item.FileName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.BaseChineseName?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (item.Tags?.Any(tag => tag?.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) == true)
                    )
                );
            }

            if (!_selectedTags.Any() && string.IsNullOrWhiteSpace(SearchText) && favorite == null)
            {
                query = Enumerable.Empty<ImageItem>();
            }

            var result = query.ToList();

            _dispatcher.Invoke(() =>
            {
                FilteredItems.Clear();
                foreach (var item in result) FilteredItems.Add(item);
                OnPropertyChanged(nameof(FilteredItemsCount));
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
                    return seriesTags != null && !string.IsNullOrEmpty(item.BaseChineseName) && !seriesTags.Contains(item.BaseChineseName);
                }
                return item.BaseChineseName == tag.DirectoryName;
            }
            return item.Tags.Contains(tag.DirectoryName);
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
                    System.Windows.Clipboard.SetImage(new BitmapImage(new Uri(item.FilePath)));
                }

                _messenger.Send(new ShowNotificationMessage("Copied to clipboard!"));

                if (!IsPinned)
                {
                    _messenger.Send(new CloseMiniWindowMessage());
                }
            }
            catch (Exception ex)
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
