using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private readonly IThumbnailService _thumbnailService;
        private HotkeyHelper? _hotkeyHelper;
        private MiniModeWindow? _miniModeWindow;
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();

        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine("MainWindow initialized.");

            // Create shared services
            var tagProvider = new TagProvider();
            _thumbnailService = new ThumbnailService();
            var messenger = new Messenger();

            // Asynchronously create and set the ViewModel
            _ = InitializeViewModelAsync(tagProvider, _thumbnailService, messenger);

            LoadingOverlay.Visibility = Visibility.Visible;
            MainContentPanel.Visibility = Visibility.Collapsed;

            // Subscribe to messages
            messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);

            // Load configuration
            LoadConfiguration();

            this.Closed += MainWindow_Closed;
        }

        private async Task InitializeViewModelAsync(ITagProvider tagProvider, IThumbnailService thumbnailService, IMessenger messenger)
        {
            _viewModel = await MainViewModel.CreateAsync(tagProvider, thumbnailService, messenger, Dispatcher);
            DataContext = _viewModel;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
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
                string configFile = Path.Combine(PathHelper.GetApplicationRoot(), "usersettings.json");
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
                string configFile = Path.Combine(PathHelper.GetApplicationRoot(), "usersettings.json");
                if (!File.Exists(configFile))
                {
                    string defaultConfig =
@"# Set to true to show a debug console on startup
debug=false

# Set language to zh-CN for Chinese, or en for English
language=zh-CN

# Set background image path (relative to the exe)
background_image=resource/zhu3.jpg

# Set to true to lock window resolution
lock_resolution=false
width=1366
height=768

# --- Mini Mode Settings ---
# Background image for the mini-mode window
mini_mode_background=resource/zhu1.png
# Resolution for the mini-mode window
mini_mode_width=640
mini_mode_height=480
# Hotkey to toggle mini-mode. Use a combination of Control, Alt, Shift, Win.
# Example: Control+Alt+B
mini_mode_hotkey=LeftCtrl+LeftAlt+B
";
                    File.WriteAllText(configFile, defaultConfig);
                }

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
                    string fullBgPath = Path.Combine(PathHelper.GetApplicationRoot(), bgPathValue.Replace('/', Path.DirectorySeparatorChar));
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
    }
}