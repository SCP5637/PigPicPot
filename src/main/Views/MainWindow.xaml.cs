using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Collections.ObjectModel;
using PigPicPot.Core;
using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Models;
using PigPicPot.Services;
using PigPicPot.Strings;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using IAnimatable = System.Windows.Media.Animation.IAnimatable;
using WpfImage = System.Windows.Controls.Image;
using WpfPanel = System.Windows.Controls.Panel;

namespace PigPicPot.Views
{
    public partial class MainWindow : Window
    {
        private PigPicPot.ViewModels.MainViewModel? _viewModel;
        private readonly ITagProvider _tagProvider;
        private readonly IThumbnailService _thumbnailService;
        private readonly IMessenger _messenger;
        private MiniModeWindow? _miniModeWindow;
        private readonly FavoriteService _favoriteService;
        private readonly ConfigurationService _configurationService;
        private readonly TrayService _trayService;
        private readonly TaskCompletionSource<bool> _initializationTcs = new TaskCompletionSource<bool>();
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();

        // 添加用于内存重置的字段
        private DateTime _lastActivityTime = DateTime.Now;
        private System.Windows.Threading.DispatcherTimer _inactivityTimer;
        private bool _isResetting = false;
        private int _inactivityResetTime = 150; // 默认150秒

        // 保存初始状态用于重置
        private System.Collections.ObjectModel.ReadOnlyCollection<ImageItem>? _initialImageItems;
        private List<TagNode>? _initialRootTags;

        // 添加初始化完成事件
        public event EventHandler? InitializationCompleted;
        public bool StartHidden { get; private set; }

        public MainWindow()
        {
            LoggingHelper.Log("MainWindow constructor called.");
            InitializeComponent();
            LoggingHelper.Log("MainWindow initialized.");

            // 初始化服务
            _messenger = new Messenger();
            _tagProvider = new TagProvider();
            _thumbnailService = new ThumbnailService();
            _favoriteService = new FavoriteService(Path.Combine(PathManager.DataRoot, "favorites.json"));
            _configurationService = new ConfigurationService();
            _trayService = new TrayService(this);

            // 订阅消息
            _messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);

            LoadConfiguration();
            LoadFavorites();

            // 确定初始可见状态
            string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
            StartHidden = ShouldStartInTray(configFile);
            
            // 读取不活动重置时间配置
            LoadInactivityResetTime(configFile);
            
            // 初始化不活动定时器
            _inactivityTimer = new System.Windows.Threading.DispatcherTimer();
            _inactivityTimer.Interval = TimeSpan.FromMinutes(1); // 每分钟检查一次
            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();

            LoggingHelper.Log("MainWindow constructor completed.");
        }

        public async Task StartInitialization()
        {
            LoggingHelper.Log("MainWindow.StartInitialization called.");
            await InitializeAsync(_tagProvider, _thumbnailService, _messenger);
        }

        public Task WaitForInitializationAsync()
        {
            LoggingHelper.Log("MainWindow.WaitForInitializationAsync called.");
            return _initializationTcs.Task;
        }

        private async Task InitializeAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger)
        {
            try
            {
                LoggingHelper.Log("MainWindow.InitializeAsync started.");
                LoggingHelper.Log("Initializing ViewModel...");
                _viewModel = await PigPicPot.ViewModels.MainViewModel.CreateAsync(tagProvider, thumbnailService, messenger, Dispatcher);
                DataContext = _viewModel;
                _favoriteService.SetViewModel(_viewModel);
                LoadFavorites();
                LoggingHelper.Log("ViewModel initialized.");

                // 保存初始状态用于重置
                _initialImageItems = _viewModel.AllImages;
                _initialRootTags = new List<TagNode>(_viewModel.Level1Tags);

                // 初始化托盘图标
                InitializeTrayIcon();

                // 检查更新
                _ = CheckForResourceUpdate();
                _ = CheckForAppUpdate();

                LoggingHelper.Log("MainWindow.InitializeAsync setting TCS result.");
                _initializationTcs.SetResult(true);
                
                // 触发初始化完成事件
                OnInitializationCompleted();
                LoggingHelper.Log("MainWindow.InitializeAsync finished.");
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error initializing ViewModel");
                _initializationTcs.SetException(ex);
            }
        }

        private void OnInitializationCompleted()
        {
            LoggingHelper.Log("MainWindow.OnInitializationCompleted called.");
            InitializationCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            LoggingHelper.Log("MainWindow closing...");
            SaveFavorites();
            _miniModeWindow?.Close();
            _inactivityTimer?.Stop(); // 停止定时器
            LoggingHelper.Log("MainWindow closed.");
        }

        // 新增：处理窗口关闭事件，改为隐藏窗口
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            LoggingHelper.Log("MainWindow closing event triggered. Hiding window instead of closing.");
            // 取消关闭操作
            e.Cancel = true;

            // 隐藏窗口
            this.Hide();

            // 显示托盘通知
            _trayService.ShowNotification("PigPicPot", "PigPicPot在你的托盘中！访问托盘以使用其主要功能。", ToolTipIcon.Info);
        }

        private void OnNotificationReceived(object sender, ShowNotificationMessage message)
        {
            LoggingHelper.Log($"Notification received: {message.Text}");
            Dispatcher.Invoke(() =>
            {
                ShowNotification(message.Text);
                // 修复：检查是否是图像加载完成的消息
                if (message.Text.Contains("图像加载完成") || message.Text.Contains("Loaded"))
                {
                    LoggingHelper.Log("Images loaded, showing main content.");
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    MainContentPanel.Visibility = Visibility.Visible;
                }
            });
        }

        private bool ShouldStartInTray(string configFile)
        {
            try
            {
                if (!File.Exists(configFile))
                    return true; // 默认改为true

                // 尝试读取JSON格式的配置文件
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                if (jsonDoc.RootElement.TryGetProperty("start_in_tray", out var startInTrayElement))
                {
                    // 修复逻辑：正确解析布尔值
                    if (startInTrayElement.ValueKind == System.Text.Json.JsonValueKind.True)
                        return true;
                    if (startInTrayElement.ValueKind == System.Text.Json.JsonValueKind.False)
                        return false;
                    if (startInTrayElement.ValueKind == System.Text.Json.JsonValueKind.String)
                        return startInTrayElement.GetString()?.ToLower() == "true";
                    return false;
                }
                return true; // 默认值改为true
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error checking start in tray setting");
                return true; // 默认改为true
            }
        }

        private void InitializeTrayIcon()
        {
            _trayService.InitializeTrayIcon();
        }

        private void ShowNotification(string message)
        {
            LoggingHelper.Log($"Showing notification: {message}");
            var notificationText = FindName("NotificationText") as TextBlock;
            var notificationOverlay = FindName("NotificationOverlay") as Border;
            if (notificationText != null) notificationText.Text = message;
            if (notificationOverlay != null && this.FindResource("NotificationStoryboard") is System.Windows.Media.Animation.Storyboard storyboard)
            {
                storyboard.Completed += (s, e) => { notificationOverlay.Visibility = Visibility.Collapsed; };
                notificationOverlay.Visibility = Visibility.Visible;
                storyboard.Begin(notificationOverlay);
            }
        }

        private async void ImageItem_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item)
            {
                // 只有当图片没有缩略图且不在处理队列中时才处理
                if (item.ThumbnailSource != null || _pendingRequests.ContainsKey(item)) return;

                // 首先尝试从数据库加载缩略图
                if (_thumbnailService is ThumbnailService thumbnailService)
                {
                    await thumbnailService.LoadThumbnailFromDatabaseAsync(item);
                }

                // 如果数据库中没有缩略图，则添加到处理队列
                if (item.ThumbnailSource == null)
                {
                    _thumbnailService.QueueThumbnailRequest(item);
                }
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

                // 当图片项从视图中卸载时，从缓存中移除以节省内存
                if (_thumbnailService is ThumbnailService thumbnailService)
                {
                    thumbnailService.RemoveFromCache(item);
                }
            }
            
            // 强制进行垃圾回收以释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        public void ToggleMiniMode()
        {
            LoggingHelper.Log("Toggling mini mode.");
            if (_miniModeWindow == null)
            {
                _miniModeWindow = new MiniModeWindow();
                _miniModeWindow.Closed += (s, e) => _miniModeWindow = null;
                _miniModeWindow.Show();
                LoggingHelper.Log("Mini mode window created and shown.");
            }
            else
            {
                if (_miniModeWindow.IsVisible)
                {
                    _miniModeWindow.Hide();
                    LoggingHelper.Log("Mini mode window hidden.");
                }
                else
                {
                    _miniModeWindow.Show();
                    _miniModeWindow.Activate();
                    LoggingHelper.Log("Mini mode window shown and activated.");
                }
            }
        }

        // 添加这个方法来处理筛选更改时的图片卸载
        private void HandleFilterChanged()
        {
            // 检查是否没有筛选（没有选中的标签且搜索文本为空）
            if (_viewModel != null && 
                !_viewModel.FavoriteTags.Any(f => f.IsSelected) &&
                string.IsNullOrEmpty(_viewModel.SearchText) &&
                _viewModel.Level1Tags.All(t => !t.IsSelected))
            {
                // 如果回到无筛选状态，执行内存重置
                ResetApplicationState();
            }
            else
            {
                // 更新活动时间
                UpdateLastActivityTime();
            }
            
            if (_thumbnailService is ThumbnailService thumbnailService)
            {
                // 获取当前可见的图片项
                var visibleItems = GetVisibleItems();
                thumbnailService.SetVisibleItems(visibleItems);
            }
        }
        
        // 添加这个方法来强制清空所有图片缓存
        private void ClearAllImageCache()
        {
            if (_thumbnailService is ThumbnailService thumbnailService)
            {
                thumbnailService.ClearAll();
            }
        }

        private void LoadConfiguration()
        {
            _configurationService.LoadConfiguration(this);
        }

        private void LoadInactivityResetTime(string configFile)
        {
            try
            {
                if (!File.Exists(configFile))
                    return;

                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                if (jsonDoc.RootElement.TryGetProperty("inactivity_reset_time", out var resetTimeElement))
                {
                    _inactivityResetTime = resetTimeElement.GetInt32();
                    LoggingHelper.Log($"Inactivity reset time loaded: {_inactivityResetTime} seconds");
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error loading inactivity reset time setting");
            }
        }

        private void ToggleUIVisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            LoggingHelper.Log("Toggling UI visibility.");
            var mainContentPanel = FindName("MainContentPanel") as DockPanel;
            if (mainContentPanel != null)
            {
                mainContentPanel.Visibility = mainContentPanel.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void ResetMemoryButton_Click(object sender, RoutedEventArgs e)
        {
            LoggingHelper.Log("Manual memory reset requested.");
            ShowNotification("正在重置内存...");
            ResetApplicationState();
            ShowNotification("内存重置完成");
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
            _favoriteService.LoadFavorites();
        }

        private void SaveFavorites()
        {
            _favoriteService.SaveFavorites();
        }

        private void Tag_Click(object sender, RoutedEventArgs e)
        {
            UpdateLastActivityTime(); // 更新活动时间
            
            if (sender is ToggleButton toggleButton && toggleButton.DataContext is TagNode tag)
            {
                tag.IsSelected = toggleButton.IsChecked == true;
                if (_viewModel != null)
                {
                    _viewModel.SelectTagCommand.Execute(tag);
                    // 处理筛选更改时的图片卸载
                    HandleFilterChanged();
                }
            }
        }

        private void FavoriteTag_Click(object sender, RoutedEventArgs e)
        {
            UpdateLastActivityTime(); // 更新活动时间
            
            if (sender is ToggleButton toggleButton &&
                toggleButton.DataContext is Favorite favorite &&
                _viewModel != null)
            {
                if (toggleButton.IsChecked == true)
                {
                    foreach (var otherFav in _viewModel.FavoriteTags.Where(f => f != favorite)) 
                        otherFav.IsSelected = false;
                    _viewModel.ApplyFilters(favorite);
                    LoggingHelper.Log($"Favorite tag selected: {favorite.Name}");
                }
                else
                {
                    _viewModel.ApplyFilters();
                    LoggingHelper.Log("Favorite tag deselected, applying default filters.");
                }
                
                // 处理筛选更改时的图片卸载
                HandleFilterChanged();
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateLastActivityTime(); // 更新活动时间
            
            if (_viewModel != null)
            {
                _viewModel.SearchText = (sender as System.Windows.Controls.TextBox)?.Text ?? string.Empty;
                // 处理筛选更改时的图片卸载
                HandleFilterChanged();
            }
        }

        private void FavoritesToggleButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateLastActivityTime(); // 更新活动时间
            
            bool isChecked = FavoritesToggleButton.IsChecked == true;
            TagsPanel.Visibility = isChecked ? Visibility.Collapsed : Visibility.Visible;
            if (FindName("FavoritesTagsPanel") is WpfPanel favoritesTagsPanel)
            {
                favoritesTagsPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_viewModel != null)
            {
                _viewModel.ClearTagSelections();
                foreach (var fav in _viewModel.FavoriteTags) fav.IsSelected = false;
                _viewModel.ApplyFilters();
                // 处理筛选更改时的图片卸载
                HandleFilterChanged();
            }
        }

        private void CreateNewFavorite_Click(object sender, RoutedEventArgs e)
        {
            LoggingHelper.Log("Creating new favorite.");
            // 使用自定义输入对话框获取用户输入
            var inputDialog = new PigPicPot.Core.InputDialog(
                PigPicPot.Strings.Resources.EnterNewFavoriteName, 
                PigPicPot.Strings.Resources.CreateFavorite, 
                "");
            
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.InputText;
                if (!string.IsNullOrWhiteSpace(newName) && _viewModel?.FavoriteTags.All(f => f.Name != newName) == true)
                {
                    LoggingHelper.Log($"Creating new favorite with name: {newName}");
                    _favoriteService.CreateNewFavorite(newName);
                }
                else if (!string.IsNullOrWhiteSpace(newName))
                {
                    LoggingHelper.Log("Failed to create new favorite: name is empty or already exists.");
                    ShowNotification(PigPicPot.Strings.Resources.FavoriteNameError);
                }
            }
        }

        private void AddImageToFavorite_Click(object sender, RoutedEventArgs e)
        {
            LoggingHelper.Log("Adding image to favorite.");
            if (_viewModel == null || (sender as FrameworkElement)?.DataContext is not ImageItem imageItem) return;

            if (_viewModel.ActiveFavoriteFilter != null)
            {
                var favorite = _viewModel.ActiveFavoriteFilter;
                _favoriteService.RemoveImageFromFavorite(favorite, imageItem);
                ShowNotification(string.Format("已从 {0} 移除", favorite.Name));
                _viewModel?.ApplyFilters(favorite);
            }
            else
            {
                var favorites = _favoriteService.GetFavoritesData().Favorites;
                // 简化实现，不使用SelectFavoriteDialog
                if (favorites.Any())
                {
                    string? selectedFavoriteName = favorites.First().Name;
                    if (!string.IsNullOrEmpty(selectedFavoriteName))
                    {
                        var selectedFavorite = favorites.FirstOrDefault(f => f.Name == selectedFavoriteName);
                        if (selectedFavorite != null)
                        {
                            _favoriteService.AddImageToFavorite(selectedFavorite, imageItem);
                            ShowNotification(string.Format("已添加到 {0}", selectedFavorite.Name));
                        }
                    }
                }
            }
        }

        private void RenameFavorite(Favorite favorite)
        {
            if (favorite == null || !favorite.IsDeletable)
            {
                LoggingHelper.Log("Cannot rename default favorite.");
                ShowNotification(PigPicPot.Strings.Resources.CannotRenameDefault);
                return;
            }

            // 使用自定义输入对话框获取用户输入
            var inputDialog = new PigPicPot.Core.InputDialog(
                string.Format(PigPicPot.Strings.Resources.RenamingFavorite, favorite.Name),
                PigPicPot.Strings.Resources.RenameFavorite,
                favorite.Name);
            
            if (inputDialog.ShowDialog() == true)
            {
                string newName = inputDialog.InputText;
                if (!string.IsNullOrWhiteSpace(newName) && _favoriteService.RenameFavorite(favorite, newName))
                {
                    ShowNotification(string.Format(PigPicPot.Strings.Resources.FavoriteRenamed, favorite.Name, newName));
                }
                else if (!string.IsNullOrWhiteSpace(newName))
                {
                    ShowNotification(PigPicPot.Strings.Resources.FavoriteNameError);
                }
            }
        }

        private void FavoriteTag_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is Favorite favorite)
            {
                // 显示上下文菜单
                if (element.ContextMenu != null)
                {
                    element.ContextMenu.Tag = favorite; // 将收藏夹对象存储在ContextMenu的Tag属性中
                    element.ContextMenu.IsOpen = true;
                }
            }
        }

        private void RenameFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.Tag is Favorite favorite)
            {
                RenameFavorite(favorite);
            }
        }

        private void DeleteFavorite_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu && contextMenu.Tag is Favorite favorite)
            {
                if (favorite.IsDeletable)
                {
                    // 构造确认消息
                    string message = string.Format(PigPicPot.Strings.Resources.ConfirmDeleteFavorite, favorite.Name);
                    
                    // 使用自定义确认对话框替代 MessageBox
                    var confirmDialog = new PigPicPot.Core.ConfirmDialog(message, PigPicPot.Strings.Resources.ConfirmDelete);
                    if (confirmDialog.ShowDialog() == true && confirmDialog.Result)
                    {
                        _favoriteService.DeleteFavorite(favorite);
                        ShowNotification(string.Format(PigPicPot.Strings.Resources.FavoriteDeleted, favorite.Name));
                    }
                }
                else
                {
                    ShowNotification(PigPicPot.Strings.Resources.CannotDeleteDefault);
                }
            }
        }

        private void ImageListBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            UpdateLastActivityTime(); // 更新活动时间
            
            if (_viewModel != null && _thumbnailService is ThumbnailService thumbnailService)
            {
                // 获取当前可见的图片项
                var visibleItems = GetVisibleItems();
                thumbnailService.SetVisibleItems(visibleItems);
                // 处理筛选更改时的图片卸载
                HandleFilterChanged();
            }
        }

        private List<ImageItem> GetVisibleItems()
        {
            var visibleItems = new List<ImageItem>();
            
            if (ImageListBox.Items.Count == 0)
                return visibleItems;

            // 获取可见区域的项
            var scrollViewer = GetScrollViewer(ImageListBox);
            if (scrollViewer == null)
                return visibleItems;

            var visibleHeight = scrollViewer.ViewportHeight;
            var verticalOffset = scrollViewer.VerticalOffset;
            var extentHeight = scrollViewer.ExtentHeight;
            
            // 如果没有滚动条或者内容高度为0，返回所有项
            if (extentHeight <= 0 || visibleHeight >= extentHeight)
            {
                foreach (ImageItem item in ImageListBox.Items)
                {
                    visibleItems.Add(item);
                }
                return visibleItems;
            }
            
            // 计算可见范围
            var startIndex = (int)(verticalOffset / 170); // 150是图片高度，加上Margin等
            var count = (int)(visibleHeight / 170) + 5; // 多加载一些以确保流畅滚动并增加缓冲区到5个项
            
            // 确保索引在有效范围内
            startIndex = Math.Max(0, startIndex);
            var endIndex = Math.Min(startIndex + count, ImageListBox.Items.Count);
            
            // 添加可见项
            for (int i = startIndex; i < endIndex; i++)
            {
                if (ImageListBox.Items[i] is ImageItem item)
                {
                    visibleItems.Add(item);
                }
            }
            
            return visibleItems;
        }

        private ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private async Task CheckForResourceUpdate()
        {
            await Task.Run(async () =>
            {
                try
                {
                    LoggingHelper.Log("Checking for resource updates...");
                    await Task.Delay(5000); // 模拟检查更新的延迟
                    LoggingHelper.Log("Resource update check completed.");
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogException(ex, "Error checking for resource updates");
                }
            });
        }

        private async Task CheckForAppUpdate()
        {
            var updateService = new UpdateService(_configurationService);
            await updateService.CheckForAppUpdate(ShowNotification);
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var infoWindow = new InfoWindow
            {
                Owner = this
            };
            infoWindow.ShowDialog();
        }

        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            // 如果设置为0，则永不触发
            if (_inactivityResetTime <= 0) return;
            
            // 检查是否超过设定时间无活动且当前没有筛选
            if (!_isResetting && DateTime.Now - _lastActivityTime > TimeSpan.FromSeconds(_inactivityResetTime))
            {
                // 检查是否没有筛选（没有选中的标签且搜索文本为空）
                if (_viewModel != null && 
                    !_viewModel.FavoriteTags.Any(f => f.IsSelected) &&
                    string.IsNullOrEmpty(_viewModel.SearchText) &&
                    _viewModel.Level1Tags.All(t => !t.IsSelected))
                {
                    // 执行内存重置
                    ResetApplicationState();
                }
            }
        }
        
        // 重置应用程序状态到初始内存占用极小的状态
        public async void ResetApplicationState()
        {
            if (_isResetting) return;
            
            _isResetting = true;
            LoggingHelper.Log("Resetting application state to reduce memory usage.");
            
            try
            {
                // 清理剪贴板临时文件
                ClipboardHelper.CleanupTempFiles();
                
                // 清空所有图片缓存
                if (_thumbnailService is ThumbnailService thumbnailService)
                {
                    thumbnailService.ClearAll();
                }
                
                // 清空所有待处理请求
                foreach (var timer in _pendingRequests.Values)
                {
                    timer.Stop();
                }
                _pendingRequests.Clear();
                
                // 如果有主窗口视图模型，重置其数据到初始状态
                if (_viewModel != null)
                {
                    _viewModel.ResetToInitialState();
                    
                    // 清理所有图片项的缩略图引用
                    if (_viewModel.AllImages != null)
                    {
                        foreach (var item in _viewModel.AllImages)
                        {
                            item.ThumbnailSource = null;
                        }
                    }
                }
                
                // 强制进行垃圾回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
                await Task.Delay(100); // 给GC一些时间
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                LoggingHelper.Log("Application state reset completed.");
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error resetting application state");
            }
            finally
            {
                _isResetting = false;
            }
        }
        
        // 更新最后活动时间
        private void UpdateLastActivityTime()
        {
            _lastActivityTime = DateTime.Now;
        }
    }
}