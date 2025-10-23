using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PigPicPot.Core;
using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.ViewModels;
using Point = System.Drawing.Point;
using Size = System.Windows.Size;
using IAnimatable = System.Windows.Media.Animation.IAnimatable;
using WpfImage = System.Windows.Controls.Image;

namespace PigPicPot.Views
{
    public partial class MiniModeWindow : Window
    {
        private FavoritesData _favoritesData = new FavoritesData();
        private readonly string _favoritesFilePath;
        private Point? _lastMousePosition;
        private readonly IThumbnailService _thumbnailService;
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();
        private MiniViewModel? _viewModel; // 添加独立的ViewModel

        public MiniModeWindow()
        {
            LoggingHelper.Log("MiniModeWindow constructor called.");
            InitializeComponent();
            LoggingHelper.Log("MiniModeWindow initialized.");

            _favoritesFilePath = Path.Combine(PathManager.DataRoot, "favorites.json");
            _thumbnailService = new ThumbnailService();
            
            var mainWindow = (System.Windows.Application.Current as App)?.MainWindow;
            if (mainWindow?.DataContext is MainViewModel mainViewModel)
            {
                // 创建独立的MiniViewModel而不是共享MainViewModel
                _viewModel = new MiniViewModel(
                    mainViewModel.TagProvider, 
                    mainViewModel.ThumbnailService, 
                    new SettingsService(),
                    mainViewModel.Messenger,
                    Dispatcher);
                DataContext = _viewModel;
                mainViewModel.Messenger.Register<FavoritesUpdatedMessage>(this, OnFavoritesUpdated);
            }
            
            LoadConfiguration();
            LoadFavorites();
            PositionWindowNearMouse();
            LoggingHelper.Log("MiniModeWindow constructor completed.");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            PositionWindowNearMouse();
        }

        private void PositionWindowNearMouse()
        {
            var point = GetMousePosition();
            _lastMousePosition = point; // 保存鼠标位置
            
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            double windowLeft = point.X - this.Width / 2;
            double windowTop = point.Y - this.Height / 2;
            if (windowLeft < 0) windowLeft = 0;
            if (windowTop < 0) windowTop = 0;
            if (windowLeft + this.Width > screenWidth) windowLeft = screenWidth - this.Width;
            if (windowTop + this.Height > screenHeight) windowTop = screenHeight - this.Height;
            this.Left = windowLeft;
            this.Top = windowTop;
        }

        private System.Drawing.Point GetMousePosition()
        {
            var point = new System.Drawing.Point();
            GetCursorPos(out point);
            return point;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out System.Drawing.Point point);

        private void LoadFavorites()
        {
            try
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

                // 修复：确保将收藏夹数据加载到MiniViewModel中
                if (_viewModel != null)
                {
                    _viewModel.FavoriteTags.Clear();
                    foreach (var fav in _favoritesData.Favorites)
                    {
                        _viewModel.FavoriteTags.Add(fav);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error loading favorites in MiniModeWindow");
                _favoritesData = new FavoritesData();
            }
        }

        private void FavoritesToggleButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = FavoritesToggleButton.IsChecked == true;
            TagsPanel.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            FavoritesTagsPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;

            if (_viewModel != null)
            {
                _viewModel.ClearTagSelections();
                foreach (var fav in _viewModel.FavoriteTags) fav.IsSelected = false;
                _viewModel.ApplyFilters();
            }
        }

        private void FavoriteTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton &&
                toggleButton.DataContext is Favorite favorite &&
                _viewModel != null)
            {
                if (toggleButton.IsChecked == true)
                {
                    foreach (var otherFav in _viewModel.FavoriteTags.Where(f => f != favorite)) otherFav.IsSelected = false;
                    _viewModel.ApplyFilters(favorite);
                }
                else
                {
                    _viewModel.ApplyFilters();
                }
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (File.Exists(configFile))
                {
                    string jsonContent = File.ReadAllText(configFile);
                    var jsonDoc = JsonDocument.Parse(jsonContent);
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("mini_mode_background", out var bgElement))
                    {
                        string bgImagePath = Path.Combine(PathManager.AppRoot, bgElement.GetString() ?? "");
                        if (File.Exists(bgImagePath))
                        {
                            BackgroundImageBrush.ImageSource = new BitmapImage(new Uri(bgImagePath));
                        }
                    }

                    if (root.TryGetProperty("mini_mode_width", out var widthElement))
                    {
                        this.Width = widthElement.GetInt32();
                    }

                    if (root.TryGetProperty("mini_mode_height", out var heightElement))
                    {
                        this.Height = heightElement.GetInt32();
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error loading mini mode configuration");
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.SearchText = (sender as System.Windows.Controls.TextBox)?.Text ?? string.Empty;
            }
        }

        private void Tag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && toggleButton.DataContext is TagNode tag)
            {
                tag.IsSelected = toggleButton.IsChecked == true;
                if (_viewModel != null)
                {
                    _viewModel.SelectTagCommand.Execute(tag);
                }
            }
        }

        private void OnFavoritesUpdated(object recipient, FavoritesUpdatedMessage message)
        {
            LoadFavorites();
        }
        
        // 添加缺失的事件处理方法
        private async void ImageItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item)
            {
                if (item.ThumbnailSource != null || _pendingRequests.ContainsKey(item)) return;

                // 首先尝试从数据库加载缩略图
                if (_thumbnailService is ThumbnailService thumbnailService)
                {
                    await thumbnailService.LoadThumbnailFromDatabaseAsync(item);
                }

                // 如果数据库中没有缩略图，则生成新的缩略图
                if (item.ThumbnailSource == null && !item.IsCorrupted)
                {
                    _thumbnailService.QueueThumbnailRequest(item);
                }
            }
        }

        private void ImageItem_Unloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item)
            {
                // 取消正在进行的缩略图请求
                if (_pendingRequests.TryGetValue(item, out var timer))
                {
                    timer.Stop();
                    _pendingRequests.Remove(item);
                }

                // 从处理队列中移除
                if (_thumbnailService is ThumbnailService thumbnailService)
                {
                    thumbnailService.RemoveFromCache(item);
                }
            }
        }

        private void Gif_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement grid && grid.DataContext is ImageItem item && item.IsAnimated)
            {
                var image = FindVisualChild<System.Windows.Controls.Image>(grid, "ThumbnailImage");
                if (image != null)
                {
                    try
                    {
                        // 使用WpfAnimatedGif库播放GIF动画
                        var bitmap = new BitmapImage(new Uri(item.FilePath));
                        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, bitmap);
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.LogException(ex, $"Error playing GIF animation for {item.FilePath}");
                    }
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
                    try
                    {
                        // 停止动画并恢复缩略图
                        WpfAnimatedGif.ImageBehavior.SetAnimatedSource(image, null);
                        image.Source = item.ThumbnailSource;
                    }
                    catch (Exception ex)
                    {
                        LoggingHelper.LogException(ex, "Error stopping GIF animation");
                        // 确保恢复缩略图
                        image.Source = item.ThumbnailSource;
                    }
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            T? foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (string.IsNullOrEmpty(childName) || typedChild is FrameworkElement fe && fe.Name == childName))
                {
                    foundChild = typedChild;
                    break;
                }

                foundChild = FindVisualChild<T>(child, childName);
                if (foundChild != null) break;
            }

            return foundChild;
        }
        
        private void ToggleBackgroundButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换背景可见性
            BackgroundImageBrush.Stretch = BackgroundImageBrush.Stretch == Stretch.None ? Stretch.UniformToFill : Stretch.None;
        }
        
        private void ResetMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            // 清理剪贴板临时文件
            ClipboardHelper.CleanupTempFiles();
            
            // 通知主窗口执行内存重置
            var app = System.Windows.Application.Current as App;
            if (app?.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ResetApplicationState();
            }
        }
    }
}