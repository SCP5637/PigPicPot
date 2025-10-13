using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using WpfAnimatedGif;

namespace PigPicPot
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly IThumbnailService _thumbnailService;
        private HotkeyHelper? _hotkeyHelper;
        private MiniModeWindow? _miniModeWindow;
        private readonly Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer> _pendingRequests = new Dictionary<ImageItem, System.Windows.Threading.DispatcherTimer>();

        public MainWindow()
        {
            InitializeComponent();
            Console.WriteLine("MainWindow initialized.");
            
            // 创建服务实例
            var tagProvider = new TagProvider();
            var thumbnailService = new ThumbnailService();
            _thumbnailService = thumbnailService;

            // 创建ViewModel
            var messenger = new Messenger();
            _viewModel = new MainViewModel(tagProvider, thumbnailService, messenger, Dispatcher);
            
            // 设置DataContext
            DataContext = _viewModel;

            LoadingOverlay.Visibility = Visibility.Collapsed;
            MainContentPanel.Visibility = Visibility.Visible;
            
            // 订阅消息
            messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);
            
            // 加载配置（在InitializeComponent之后）
            LoadConfiguration();
            
            this.Closed += MainWindow_Closed;
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
                Console.WriteLine($"Using hotkey: {hotkeyStr}");

                var parts = hotkeyStr.Split('+');
                if (parts.Length < 2) throw new ArgumentException("Hotkey must include at least one modifier and a key.");

                var key = (Key)Enum.Parse(typeof(Key), parts.Last(), true);
                Console.WriteLine($"Parsed key: {key}");
                
                ModifierKeys modifiers = ModifierKeys.None;
                foreach (var modStr in parts.Take(parts.Length - 1))
                {
                    // 处理左右修饰键的情况
                    if (modStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("LeftCtrl", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("RightCtrl", StringComparison.OrdinalIgnoreCase))
                        modifiers |= ModifierKeys.Control;
                    if (modStr.Contains("Alt", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("LeftAlt", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("RightAlt", StringComparison.OrdinalIgnoreCase))
                        modifiers |= ModifierKeys.Alt;
                    if (modStr.Contains("Shift", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("LeftShift", StringComparison.OrdinalIgnoreCase) ||
                        modStr.Contains("RightShift", StringComparison.OrdinalIgnoreCase))
                        modifiers |= ModifierKeys.Shift;
                    if (modStr.Contains("Win", StringComparison.OrdinalIgnoreCase))
                        modifiers |= ModifierKeys.Windows;
                }
                Console.WriteLine($"Parsed modifiers: {modifiers}");
                
                _hotkeyHelper.Register(modifiers, key, ToggleMiniMode);
                Console.WriteLine($"Hotkey '{hotkeyStr}' registered successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register hotkey: {ex.Message}");
                System.Windows.MessageBox.Show($"Failed to register hotkey: {ex.Message}", "Error");
            }
        }

        private string GetHotkeyFromConfig()
        {
            // 默认热键
            string defaultHotkey = "LeftCtrl+LeftAlt+B";
            
            try
            {
                // 首先尝试从应用程序目录读取配置
                string configFile = Path.Combine(PathHelper.GetApplicationRoot(), "usersettings.json");
                Console.WriteLine($"Reading config from: {configFile}");
                Console.WriteLine($"Config file exists: {File.Exists(configFile)}");
                
                if (!File.Exists(configFile))
                {
                    // 如果应用程序目录没有配置文件，尝试项目目录
                    string projectConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "usersettings.json");
                    projectConfigFile = Path.GetFullPath(projectConfigFile);
                    Console.WriteLine($"Trying project config: {projectConfigFile}");
                    Console.WriteLine($"Project config exists: {File.Exists(projectConfigFile)}");
                    
                    if (File.Exists(projectConfigFile))
                    {
                        configFile = projectConfigFile;
                    }
                    else
                    {
                        Console.WriteLine("No config file found, using default hotkey.");
                        return defaultHotkey;
                    }
                }

                var allLines = File.ReadAllLines(configFile);
                Console.WriteLine($"Config file lines: {allLines.Length}");
                
                var config = allLines
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                    .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                Console.WriteLine($"Config dictionary count: {config.Count}");
                foreach (var kvp in config)
                {
                    Console.WriteLine($"  Config key: '{kvp.Key}', value: '{kvp.Value}'");
                }

                if (config.TryGetValue("mini_mode_hotkey", out var hotkeyStr))
                {
                    Console.WriteLine($"Found hotkey config: {hotkeyStr}");
                    return hotkeyStr;
                }
                else
                {
                    Console.WriteLine("No mini_mode_hotkey found in config, using default.");
                    return defaultHotkey;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading config: {ex.Message}, using default hotkey.");
                return defaultHotkey;
            }
        }

        private void OnNotificationReceived(object sender, ShowNotificationMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowNotification(message.Text);
                
                // 如果消息包含"Loaded"，则隐藏加载界面
                if (message.Text.Contains("Loaded"))
                {
                    var loadingOverlay = FindName("LoadingOverlay") as Grid;
                    if (loadingOverlay != null)
                    {
                        loadingOverlay.Visibility = Visibility.Collapsed;
                    }
                    
                    var mainContentPanel = FindName("MainContentPanel") as DockPanel;
                    if (mainContentPanel != null)
                    {
                        mainContentPanel.Visibility = Visibility.Visible;
                    }
                }
            });
        }

        private void ShowNotification(string message)
        {
            var notificationText = FindName("NotificationText") as TextBlock;
            var notificationOverlay = FindName("NotificationOverlay") as Border;
            
            if (notificationText != null)
            {
                notificationText.Text = message;
            }
            
            if (notificationOverlay != null && this.FindResource("NotificationStoryboard") is Storyboard storyboard)
            {
                storyboard.Completed += (s, e) => { notificationOverlay.Visibility = Visibility.Collapsed; };
                notificationOverlay.Visibility = Visibility.Visible;
                storyboard.Begin(notificationOverlay);
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
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
            Console.WriteLine("--- Hotkey pressed! ToggleMiniMode() was called. ---");
            if (_miniModeWindow == null)
            {
                _miniModeWindow = new MiniModeWindow();
                _miniModeWindow.Closed += (s, e) => _miniModeWindow = null;
                _miniModeWindow.Show();
            }
            else
            {
                if (_miniModeWindow.IsVisible)
                {
                    _miniModeWindow.Hide();
                }
                else
                {
                    _miniModeWindow.Show();
                    _miniModeWindow.Activate();
                }
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                string configFile = Path.Combine(PathHelper.GetApplicationRoot(), "usersettings.json");
                if (!File.Exists(configFile))
                {
                    Console.WriteLine("Config file not found, creating default.");
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

                // 设置窗口大小
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

                // 设置背景图片
                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathHelper.GetApplicationRoot(), bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        
                        // 使用FindName获取控件引用
                        var backgroundBrush = FindName("BackgroundImageBrush") as ImageBrush;
                        if (backgroundBrush != null)
                        {
                            backgroundBrush.ImageSource = bgBitmap;
                        }

                        const string ExpectedHash = "0628b9d8d23d7a695938425fef17f9da4643246f4e410ab44461bdae8349a303";
                        string currentHash = ComputeFileHash(fullBgPath);
                        if (Path.GetFileName(fullBgPath).Equals("zhu3.jpg", StringComparison.OrdinalIgnoreCase) &&
                            bgBitmap.PixelWidth == 1920 && bgBitmap.PixelHeight == 1176 &&
                            currentHash.Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            var infoButton = FindName("InfoButton") as System.Windows.Controls.Button;
                            if (infoButton != null)
                            {
                                infoButton.Visibility = Visibility.Visible;
                            }
                            Console.WriteLine("Special background detected. Info button enabled.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                this.Width = 1366;
                this.Height = 768;
            }
        }

        private void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var infoWindow = new InfoWindow
            {
                Owner = this
            };
            infoWindow.ShowDialog();
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
                    if (controller != null)
                    {
                        controller.Play();
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
                    var controller = ImageBehavior.GetAnimationController(image);
                    if (controller != null)
                    {
                        controller.Pause();
                    }
                    // Detach the animated source to release the file and stop processing
                    ImageBehavior.SetAnimatedSource(image, null);
                    // Restore the thumbnail
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
