using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PigPicPot.Core;
using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.ViewModels;
using WpfAnimatedGif;

namespace PigPicPot.Views
{
    public class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private readonly IThumbnailService _thumbnailService;
        private HotkeyHelper? _hotkeyHelper;
        private MiniModeWindow? _miniModeWindow;
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();

        private FavoritesData _favoritesData = new FavoritesData();
        private readonly string _favoritesFilePath;

        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine("MainWindow initialized.");

            var tagProvider = new TagProvider();
            _thumbnailService = new ThumbnailService();
            var messenger = new Messenger();

            _ = InitializeViewModelAsync(tagProvider, _thumbnailService, messenger);

            LoadingOverlay.Visibility = Visibility.Visible;
            MainContentPanel.Visibility = Visibility.Collapsed;

            messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);

            _favoritesFilePath = Path.Combine(PathManager.DataRoot, "favorites.json");
            
            LoadConfiguration();
            LoadFavorites();

            this.Closed += MainWindow_Closed;

            _ = CheckForResourceUpdate();
            _ = CheckForAppUpdate();
        }

        private async Task InitializeViewModelAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger)
        {
            _viewModel = await MainViewModel.CreateAsync(tagProvider, thumbnailService, messenger, Dispatcher);
            DataContext = _viewModel;
            LoadFavorites();
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            SaveFavorites();
            _hotkeyHelper?.Dispose();
            _miniModeWindow?.Close();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("MainWindow_Loaded event triggered.");
            _hotkeyHelper = new HotkeyHelper(this);
            try
            {
                string hotkeyStr = GetHotkeyFromConfig();
                var parts = hotkeyStr.Split('+');
                if (parts.Length < 2) throw new ArgumentException("Hotkey must include at least one modifier and a key.");

                var key = (Key)Enum.Parse(typeof(Key), parts.Last(), true);
                ModifierKeys modifiers = ModifierKeys.None;
                foreach (var modStr in parts.Take(parts.Length - 1))
                {
                    if (modStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
                    if (modStr.Contains("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
                    if (modStr.Contains("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
                    if (modStr.Contains("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;
                }
                _hotkeyHelper.Register(modifiers, key, ToggleMiniMode);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to register hotkey: {ex.Message}", "Error");
            }
        }

        private string GetHotkeyFromConfig()
        {
            string defaultHotkey = "LeftCtrl+LeftAlt+B";
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile)) return defaultHotkey;

                var config = File.ReadAllLines(configFile)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                    .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                return config.TryGetValue("mini_mode_hotkey", out var hotkeyStr) ? hotkeyStr : defaultHotkey;
            }
            catch (Exception)
            {
                return defaultHotkey;
            }
        }

        private void OnNotificationReceived(object sender, ShowNotificationMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowNotification(message.Text);
                if (message.Text.Contains("Loaded"))
                {
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MainContentPanel.Visibility = Visibility.Visible;
                }
            });
        }

        private void ShowNotification(string message)
        {
            var notificationText = FindName("NotificationText") as TextBlock;
            var notificationOverlay = FindName("NotificationOverlay") as Border;
            if (notificationText != null) notificationText.Text = message;
            if (notificationOverlay != null && this.FindResource("NotificationStoryboard") is Storyboard storyboard)
            {
                storyboard.Completed += (s, e) => { notificationOverlay.Visibility = Visibility.Collapsed; };
                notificationOverlay.Visibility = Visibility.Visible;
                storyboard.Begin(notificationOverlay);
            }
        }

        private void ImageItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item)
            {
                if (item.ThumbnailSource != null || _pendingRequests.ContainsKey(item)) return;
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    _pendingRequests.Remove(item);
                    _thumbnailService.QueueThumbnailRequest(item);
                };
                _pendingRequests[item] = timer;
                timer.Start();
            }
        }

        private void ImageItem_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item)
            {
                if (_pendingRequests.TryGetValue(item, out var timer))
                {
                    timer.Stop();
                    _pendingRequests.Remove(item);
                }
            }
        }

        private void ToggleMiniMode()
        {
            if (_miniModeWindow == null)
            {
                _miniModeWindow = new MiniModeWindow();
                _miniModeWindow.Closed += (s, e) => _miniModeWindow = null;
                _miniModeWindow.Show();
            }
            else
            {
                if (_miniModeWindow.IsVisible) _miniModeWindow.Hide();
                else { _miniModeWindow.Show(); _miniModeWindow.Activate(); }
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile)) return;

                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                if (config.TryGetValue("lock_resolution", out var lockResStr) && lockResStr == "true")
                {
                    this.Width = config.TryGetValue("width", out var w) && int.TryParse(w, out int width) ? width : 1366;
                    this.Height = config.TryGetValue("height", out var h) && int.TryParse(h, out int height) ? height : 768;
                    this.ResizeMode = ResizeMode.NoResize;
                }
                else
                {
                    this.Width = 1366;
                    this.Height = 768;
                    this.ResizeMode = ResizeMode.CanResize;
                }

                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathManager.AppRoot, bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        var backgroundBrush = FindName("BackgroundImageBrush") as ImageBrush;
                        if (backgroundBrush != null) backgroundBrush.ImageSource = bgBitmap;
                        SpecialFeatures.CheckAndEnableFeatures(this, fullBgPath, bgBitmap.PixelWidth, bgBitmap.PixelHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading configuration: {ex.Message}");
            }
        }

        private void ToggleUIVisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            var mainContentPanel = FindName("MainContentPanel") as DockPanel;
            if (mainContentPanel != null)
            {
                mainContentPanel.Visibility = mainContentPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void Gif_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement grid && grid.DataContext is ImageItem item && item.IsAnimated && !item.IsCorrupted)
            {
                var image = FindVisualChild<System.Windows.Controls.Image>(grid, "ThumbnailImage");
                if (image != null)
                {
                    ImageBehavior.SetAnimatedSource(image, new BitmapImage(new Uri(item.FilePath)));
                    var controller = ImageBehavior.GetAnimationController(image);
                    controller?.Play();
                }
            }
        }

        private void Gif_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement grid && grid.DataContext is ImageItem item && item.IsAnimated)
            {
                var image = FindVisualChild<System.Windows.Controls.Image>(grid, "ThumbnailImage");
                if (image != null)
                {
                    var controller = ImageBehavior.GetAnimationController(image);
                    controller?.Pause();
                    ImageBehavior.SetAnimatedSource(image, null);
                    image.Source = item.ThumbnailSource;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject? parent, string childName) where T : FrameworkElement
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t && t.Name == childName) return t;
                var childOfChild = FindVisualChild<T>(child, childName);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        private void LoadFavorites()
        {
            if (File.Exists(_favoritesFilePath))
            {
                var json = File.ReadAllText(_favoritesFilePath);
                _favoritesData = JsonSerializer.Deserialize<FavoritesData>(json) ?? new FavoritesData();

                bool favoritesModified = false;
                foreach (var favorite in _favoritesData.Favorites)
                {
                    foreach (var image in favorite.Images)
                    {
                        if (!File.Exists(image.FilePath))
                        {
                            var foundImage = FindImageByHash(image.Hash);
                            if (foundImage != null)
                            {
                                image.FilePath = foundImage.FilePath;
                                favoritesModified = true;
                            }
                        }
                    }
                }

                if (favoritesModified)
                {
                    SaveFavorites();
                }
            }
            else
            {
                _favoritesData = new FavoritesData
                {
                    Favorites = new List<Favorite>
                    {
                        new Favorite { Name = "Default", IsDeletable = false, Images = new List<FavoriteImage>() }
                    }
                };
                SaveFavorites();
            }

            if (_viewModel != null)
            {
                _viewModel.FavoriteTags.Clear();
                foreach (var fav in _favoritesData.Favorites)
                {
                    _viewModel.FavoriteTags.Add(fav);
                }
            }
        }

        private ImageItem? FindImageByHash(string hash)
        {
            if (_viewModel == null || string.IsNullOrEmpty(hash))
            {
                return null;
            }

            foreach (var imageItem in _viewModel.AllImages)
            {
                string itemHash = ComputeFileHash(imageItem.FilePath);
                if (itemHash == hash)
                {
                    return imageItem;
                }
            }
            return null;
        }

        private void SaveFavorites()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_favoritesData, options);
                File.WriteAllText(_favoritesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save favorites: {ex.Message}", "Error");
            }
        }

        private void FavoritesToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = FavoritesToggleButton.IsChecked == true;
            TagsPanel.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            FavoritesTagsItemsControl.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            if (_viewModel != null)
            {
                _viewModel.ClearTagSelections();
                foreach (var fav in _viewModel.FavoriteTags) fav.IsSelected = false;
                _viewModel.ApplyFilters();
            }
        }

        private void NewFavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            var inputDialog = new InputDialog(PigPicPot.Strings.Resources.EnterNewFavoriteName, PigPicPot.Strings.Resources.CreateFavorite);
            inputDialog.Owner = this;
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.ResponseText ?? "";
                if (!string.IsNullOrWhiteSpace(newName) && _favoritesData.Favorites.All(f => f.Name != newName))
                {
                    var newFavorite = new Favorite { Name = newName, IsDeletable = true, Images = new List<FavoriteImage>() };
                    _favoritesData.Favorites.Add(newFavorite);
                    _viewModel?.FavoriteTags.Add(newFavorite);
                    SaveFavorites();
                }
                else
                {
                    System.Windows.MessageBox.Show(PigPicPot.Strings.Resources.FavoriteNameError, PigPicPot.Strings.Resources.Error);
                }
            }
        }

        private void AddImageToFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null || (sender as FrameworkElement)?.DataContext is not ImageItem imageItem) return;

            if (_viewModel.ActiveFavoriteFilter != null)
            {
                var favorite = _viewModel.ActiveFavoriteFilter;
                var imageToRemove = favorite.Images.FirstOrDefault(img => img.FilePath == imageItem.FilePath);
                if (imageToRemove != null)
                {
                    favorite.Images.Remove(imageToRemove);
                    SaveFavorites();
                    ShowNotification(string.Format(PigPicPot.Strings.Resources.RemovedFromFavorite, favorite.Name));
                    _viewModel?.ApplyFilters(favorite);
                }
            }
            else
            {
                var dialog = new SelectFavoriteDialog(_favoritesData.Favorites.Select(f => f.Name).ToList());
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    string? selectedFavoriteName = dialog.SelectedFavorite;
                    if (selectedFavoriteName == null) return;

                    var favorite = _favoritesData.Favorites.FirstOrDefault(f => f.Name == selectedFavoriteName);
                    if (favorite != null)
                    {
                        if (favorite.Images.Any(img => img.FilePath == imageItem.FilePath))
                        {
                            ShowNotification(string.Format(PigPicPot.Strings.Resources.ImageAlreadyInFavorite, favorite.Name));
                            return;
                        }

                        var favoriteImage = new FavoriteImage
                        {
                            FilePath = imageItem.FilePath,
                            FileName = imageItem.FileName,
                            Hash = ComputeFileHash(imageItem.FilePath)
                        };
                        favorite.Images.Add(favoriteImage);
                        SaveFavorites();
                        ShowNotification(string.Format(PigPicPot.Strings.Resources.AddedToFavorite, favorite.Name));
                    }
                }
            }
        }

        private void RenameFavorite(Favorite favorite)
        {
            System.Diagnostics.Debug.WriteLine($"RenameFavorite called with favorite: {favorite?.Name ?? "null"}");
            if (!favorite.IsDeletable)
            {
                System.Windows.MessageBox.Show(PigPicPot.Strings.Resources.CannotRenameDefault, PigPicPot.Strings.Resources.Error);
                return;
            }

            var inputDialog = new InputDialog(string.Format(PigPicPot.Strings.Resources.RenamingFavorite, favorite.Name), PigPicPot.Strings.Resources.RenameFavorite);
            inputDialog.Owner = this;
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.ResponseText ?? "";
                if (!string.IsNullOrWhiteSpace(newName) && _favoritesData.Favorites.All(f => f.Name != newName))
                {
                    string oldName = favorite.Name;
                    favorite.Name = newName;
                    
                    SaveFavorites();
                    
                    ShowNotification(string.Format(PigPicPot.Strings.Resources.FavoriteRenamed, oldName, newName));
                }
                else
                {
                    System.Windows.MessageBox.Show(PigPicPot.Strings.Resources.FavoriteNameError, PigPicPot.Strings.Resources.Error);
                }
            }
        }

        private void DeleteFavorite(Favorite favorite)
        {
            System.Diagnostics.Debug.WriteLine($"DeleteFavorite called with favorite: {favorite?.Name ?? "null"}");
            if (!favorite.IsDeletable)
            {
                System.Windows.MessageBox.Show(PigPicPot.Strings.Resources.CannotDeleteDefault, PigPicPot.Strings.Resources.Error);
                return;
            }

            if (System.Windows.MessageBox.Show(
                string.Format(PigPicPot.Strings.Resources.ConfirmDeleteFavorite, favorite.Name), 
                PigPicPot.Strings.Resources.ConfirmDelete, 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _favoritesData.Favorites.Remove(favorite);
                _viewModel?.FavoriteTags.Remove(favorite);
                SaveFavorites();
                
                ShowNotification(string.Format(PigPicPot.Strings.Resources.FavoriteDeleted, favorite.Name));
            }
        }

        private string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private void FavoriteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && toggleButton.DataContext is Favorite favorite)
            {
                if (_viewModel == null) return;

                if (toggleButton.IsChecked == true)
                {
                    foreach (var otherFav in _viewModel.FavoriteTags.Where(f => f != favorite))
                    {
                        otherFav.IsSelected = false;
                    }
                    _viewModel.ApplyFilters(favorite);
                }
                else
                {
                    _viewModel.ApplyFilters();
                }
            }
        }

        private void FavoriteTag_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && 
                toggleButton.DataContext is Favorite favorite)
            {
                ContextMenu contextMenu = new ContextMenu();

                MenuItem renameItem = new MenuItem();
                renameItem.Header = PigPicPot.Strings.Resources.Rename;
                renameItem.Click += (s, args) => RenameFavorite(favorite);
                contextMenu.Items.Add(renameItem);

                MenuItem deleteItem = new MenuItem();
                deleteItem.Header = PigPicPot.Strings.Resources.Delete;
                deleteItem.Click += (s, args) => DeleteFavorite(favorite);
                contextMenu.Items.Add(deleteItem);

                toggleButton.ContextMenu = contextMenu;
                contextMenu.IsOpen = true;
            }
        }

        private void FavoriteTag_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton)
            {
                if (toggleButton.ContextMenu != null)
                {
                    toggleButton.ContextMenu.PlacementTarget = toggleButton;
                    toggleButton.ContextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }

        private async Task CheckForResourceUpdate()
        {
            var config = GetConfig();
            if (config.TryGetValue("check_for_updates", out var check) && check.ToLower() == "false")
            {
                return;
            }

            try
            {
                string versionPath = Path.Combine(PathManager.AppRoot, "resource", "version.txt");
                if (!File.Exists(versionPath)) return;
                string localVersion = File.ReadAllText(versionPath).Trim();

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PigPicPot");
                    var response = await client.GetAsync("https://api.github.com/repos/JodieRuth/PigPicPot/releases/latest");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                    if (release == null) return;
                    string latestVersion = release.TagName;

                    if (new Version(localVersion.TrimStart('v')) < new Version(latestVersion.TrimStart('v')))
                    {
                        ShowNotification("Resource update available!");
                        Process.Start(new ProcessStartInfo("https://github.com/JodieRuth/PigPicPot/releases/latest") { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Resource update check failed: {ex.Message}");
            }
        }

        private async Task CheckForAppUpdate()
        {
            var config = GetConfig();
            if (config.TryGetValue("check_for_updates", out var check) && check.ToLower() == "false")
            {
                return;
            }

            try
            {
                string currentVersion = "v0.4";
                File.WriteAllText(Path.Combine(PathManager.AppRoot, "version.txt"), currentVersion);

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "PigPicPot");
                    var response = await client.GetAsync("https://api.github.com/repos/SCP5637/PigPicPot/releases/latest");
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var release = JsonSerializer.Deserialize<GitHubRelease>(json);
                    if (release == null) return;
                    string latestVersion = release.TagName;

                    if (new Version(currentVersion.TrimStart('v')) < new Version(latestVersion.TrimStart('v')))
                    {
                        ShowNotification("Application update available!");
                        Process.Start(new ProcessStartInfo("https://github.com/SCP5637/PigPicPot/releases/latest") { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"App update check failed: {ex.Message}");
            }
        }

        private Dictionary<string, string> GetConfig()
        {
            string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
            if (!File.Exists(configFile)) return new Dictionary<string, string>();

            return File.ReadAllLines(configFile)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());
        }
    }

    public class InputDialog : Window
    {
        public string? ResponseText { get; private set; }
        private System.Windows.Controls.TextBox _textBox;

        public InputDialog(string question, string title)
        {
            Title = title;
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = question, Margin = new Thickness(0, 0, 0, 10) });
            _textBox = new System.Windows.Controls.TextBox();
            panel.Children.Add(_textBox);

            var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            okButton.Click += (s, e) => { ResponseText = _textBox.Text; DialogResult = true; };
            buttonPanel.Children.Add(okButton);
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(buttonPanel);
            Content = panel;
        }
    }

    public class SelectFavoriteDialog : Window
    {
        public string? SelectedFavorite { get; private set; }
        private System.Windows.Controls.ComboBox _comboBox;

        public SelectFavoriteDialog(List<string> favorites)
        {
            Title = "Select Favorite";
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var panel = new StackPanel { Margin = new Thickness(10) };
            panel.Children.Add(new TextBlock { Text = "Add to which favorite?", Margin = new Thickness(0, 0, 0, 10) });
            _comboBox = new System.Windows.Controls.ComboBox { ItemsSource = favorites };
            if (favorites.Any()) _comboBox.SelectedIndex = 0;
            panel.Children.Add(_comboBox);

            var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(5) };
            okButton.Click += (s, e) => { SelectedFavorite = _comboBox.SelectedItem as string; DialogResult = true; };
            buttonPanel.Children.Add(okButton);
            var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, Margin = new Thickness(5) };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(buttonPanel);
            Content = panel;
        }
    }
}