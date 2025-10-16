using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using WpfAnimatedGif;
using PigPicPot.Core;
using PigPicPot.Models;
using PigPicPot.ViewModels;
using PigPicPot.Services;
using PigPicPot.Helpers;
using PigPicPot.Messaging;

namespace PigPicPot.Views
{
    public partial class MiniModeWindow : Window
    {
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();
        private FavoritesData _favoritesData = new FavoritesData();
        private readonly string _favoritesFilePath;

        public MiniModeWindow()
        {
            InitializeComponent();
            LoadMiniModeConfiguration();

            var tagProvider = new TagProvider();
            var thumbnailService = new ThumbnailService();
            var settingsService = new SettingsService();
            var messenger = new Messenger();

            var viewModel = new MiniViewModel(tagProvider, thumbnailService, settingsService, messenger, Dispatcher);
            DataContext = viewModel;

            messenger.Register<CloseMiniWindowMessage>(this, (recipient, message) => this.Close());
            messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);

            _favoritesFilePath = Path.Combine(PathManager.DataRoot, "favorites.json");
            LoadFavorites();
        }

        private void LoadFavorites()
        {
            if (File.Exists(_favoritesFilePath))
            {
                var json = File.ReadAllText(_favoritesFilePath);
                _favoritesData = JsonSerializer.Deserialize<FavoritesData>(json) ?? new FavoritesData();
            }
            else
            {
                _favoritesData = new FavoritesData();
            }

            if (DataContext is MiniViewModel viewModel)
            {
                viewModel.FavoriteTags.Clear();
                foreach (var fav in _favoritesData.Favorites)
                {
                    viewModel.FavoriteTags.Add(fav);
                }
            }
        }

        private void FavoritesToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = FavoritesToggleButton.IsChecked == true;
            TagsPanel.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            FavoritesTagsPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            if (DataContext is MiniViewModel viewModel)
            {
                viewModel.ClearTagSelections();
                foreach (var fav in viewModel.FavoriteTags) fav.IsSelected = false;
                viewModel.ApplyFilters();
            }
        }

        private void FavoriteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton &&
                toggleButton.DataContext is Favorite favorite &&
                DataContext is MiniViewModel viewModel)
            {
                if (toggleButton.IsChecked == true)
                {
                    foreach (var otherFav in viewModel.FavoriteTags.Where(f => f != favorite)) otherFav.IsSelected = false;
                    viewModel.ApplyFilters(favorite);
                }
                else
                {
                    viewModel.ApplyFilters();
                }
            }
        }

        private void LoadMiniModeConfiguration()
        {
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile)) return;

                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                this.Width = config.TryGetValue("mini_mode_width", out var w) && int.TryParse(w, out int width) ? width : 640;
                this.Height = config.TryGetValue("mini_mode_height", out var h) && int.TryParse(h, out int height) ? height : 480;

                if (config.TryGetValue("mini_mode_background", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathManager.AppRoot, bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();

                        if (FindName("BackgroundImageBrush") is ImageBrush backgroundBrush) backgroundBrush.ImageSource = bgBitmap;
                        SpecialFeatures.CheckAndEnableFeatures(this, fullBgPath, bgBitmap.PixelWidth, bgBitmap.PixelHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading mini-mode configuration: {ex.Message}");
                this.Width = 640;
                this.Height = 480;
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
                    ImageBehavior.GetAnimationController(image)?.Play();
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
                    ImageBehavior.GetAnimationController(image)?.Pause();
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

        private void OnNotificationReceived(object sender, ShowNotificationMessage message)
        {
            Dispatcher.Invoke(() => ShowNotification(message.Text));
        }

        private void ShowNotification(string message)
        {
            var notificationText = FindName("NotificationText") as TextBlock;
            var notificationOverlay = FindName("NotificationOverlay") as Border;

            if (notificationText != null) notificationText.Text = message;

            if (notificationOverlay != null && this.Resources["NotificationStoryboard"] is Storyboard storyboard)
            {
                storyboard.Completed += (s, e) => { notificationOverlay.Visibility = Visibility.Collapsed; };
                notificationOverlay.Visibility = Visibility.Visible;
                storyboard.Begin(notificationOverlay);
            }
        }

        private void Image_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item) return;

            try
            {
                if (item.IsAnimated)
                {
                    ClipboardHelper.SetAnimatedGif(item.FilePath);
                    ShowNotification(PigPicPot.Strings.Resources.ImageCopiedNotification);
                }
                else
                {
                    System.Windows.Clipboard.SetImage(new BitmapImage(new Uri(item.FilePath)));
                    ShowNotification(PigPicPot.Strings.Resources.ImageCopiedNotification);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"复制失败: {ex.Message}", "Error");
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
                    if (DataContext is MiniViewModel viewModel) viewModel.ThumbnailService?.QueueThumbnailRequest(item);
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
    }
}