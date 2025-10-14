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

        // Statistics properties
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

        // Private constructor for the factory method
        private MainViewModel(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher? dispatcher = null)
        {
            _tagProvider = tagProvider;
            _thumbnailService = thumbnailService;
            _messenger = messenger;
            _dispatcher = dispatcher ?? Dispatcher.CurrentDispatcher;

            SelectTagCommand = new RelayCommand<TagNode>(SelectTag);
            CopyImageCommand = new RelayCommand<ImageItem>(CopyImage);
        }

        // Public factory method for async initialization
        public static async Task<MainViewModel> CreateAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger, Dispatcher dispatcher)
        {
            var viewModel = new MainViewModel(tagProvider, thumbnailService, messenger, dispatcher);
            await viewModel.InitializeAsync();
            return viewModel;
        }

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

            // Start initial background loading of all thumbnails
            _thumbnailService.Prioritize(System.Linq.Enumerable.Empty<ImageItem>(), _allImageItems);
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

        private void ApplyFilters()
        {
            if (_allImageItems == null) return;

            IEnumerable<ImageItem> query = _allImageItems;

            if (_selectedTags.Count > 0)
            {
                query = query.Where(item => _selectedTags.All(tag => IsItemInTag(item, tag)));
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

            if (_selectedTags.Count == 0 && string.IsNullOrWhiteSpace(SearchText))
            {
                query = Enumerable.Empty<ImageItem>();
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
