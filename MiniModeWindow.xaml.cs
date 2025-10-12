using System;
using System.Collections.Generic;
using System.IO;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Diagnostics;

namespace PigPicPot
{
    public partial class MiniModeWindow : Window
    {
        private readonly MainWindow _mainWindow;
        private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "run_log.txt");

        public MiniModeWindow(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
            LoadMiniModeConfiguration();
            SyncStateFromMainWindow();

            // Subscribe to future changes
            _mainWindow.FilterChanged += OnFilterChanged;
            this.Closed += (s, e) => _mainWindow.FilterChanged -= OnFilterChanged;

            // Wire up UI events to call MainWindow methods
             SearchTextBox.TextChanged += (s, e) => _mainWindow.SetSearchText(SearchTextBox.Text);
            StaticFilterButton.Click += (s, e) => _mainWindow.SetMainFilter(StaticFilterButton.IsChecked == true ? "Static" : "");
            DynamicFilterButton.Click += (s, e) => _mainWindow.SetMainFilter(DynamicFilterButton.IsChecked == true ? "Dynamic" : "");
            ZhuxxFilterButton.Click += (s, e) => HandleSubFilterClick(ZhuxxFilterButton, "zhuxx");
            OtherFilterButton.Click += (s, e) => HandleSubFilterClick(OtherFilterButton, "Other");
            AnimeFilterButton.Click += (s, e) => HandleSubFilterClick(AnimeFilterButton, "anime");
            RealFilterButton.Click += (s, e) => HandleSubFilterClick(RealFilterButton, "real");

            // 重定向控制台输出
            var filestream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
            var streamWriter = new StreamWriter(filestream) { AutoFlush = true };
            Console.SetOut(streamWriter);
            Console.SetError(streamWriter); // Use Console.SetError
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out)); // Use Trace.Listeners

        }

        private void HandleSubFilterClick(ToggleButton button, string filterName)
        {
            _mainWindow.SetSubFilter(button.IsChecked == true ? filterName : "");
        }

        private void OnFilterChanged(IEnumerable<ImageItem> newItems)
        {
            ImageListBox.ItemsSource = newItems;
            int totalCount = _mainWindow.AllImageItems.Count();
            int filteredCount = newItems.Count();
            SummaryTextBlock.Text = $"共 {filteredCount} 个结果 / 总计 {totalCount} 个";
            SyncStateFromMainWindow(); // Re-sync button states etc.
        }

        private void SyncStateFromMainWindow()
        {
            // Sync Search Text
            if (SearchTextBox.Text != _mainWindow.CurrentSearchText)
            {
                SearchTextBox.Text = _mainWindow.CurrentSearchText;
            }

            // Sync Main Filters
            StaticFilterButton.IsChecked = _mainWindow.ActiveMainFilter == "Static";
            DynamicFilterButton.IsChecked = _mainWindow.ActiveMainFilter == "Dynamic";

            StaticSubFilterPanel.Visibility = StaticFilterButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            DynamicSubFilterPanel.Visibility = DynamicFilterButton.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            // Sync Sub Filters
            SyncToggleButtonState(StaticSubFilterPanel, _mainWindow.ActiveSubFilter);
            SyncToggleButtonState(DynamicSubFilterPanel, _mainWindow.ActiveSubFilter);

            // Sync Level 3 Filters
            PopulateLevel3Filters(_mainWindow.ActiveSubFilter);
            SyncToggleButtonState(Level3FilterPanel, _mainWindow.ActiveLevel3Filter, true);
        }

        private void SyncToggleButtonState(System.Windows.Controls.Panel panel, string activeFilter, bool useTag = false)
        {
            foreach (ToggleButton btn in panel.Children)
            {
                string? btnFilter = useTag ? btn.Tag as string : btn.Name.Replace("FilterButton", "").ToLower();
                btn.IsChecked = btnFilter == activeFilter;
            }
        }

        private void PopulateLevel3Filters(string category)
        {
            if (string.IsNullOrEmpty(category))
            {
                Level3ScrollViewer.Visibility = Visibility.Collapsed;
                return;
            }

            Level3FilterPanel.Children.Clear();
            
            bool hasSingletons = _mainWindow.AllImageItems.Any(i => i.FilePath.Contains("\\" + category + "\\") && i.IsSingleton);

            if (hasSingletons)
            {
                var otherButton = new ToggleButton
                {
                    Content = _mainWindow.CurrentLanguage == "zh-CN" ? "其他" : "Other",
                    Tag = "_OTHER_",
                    Margin = new Thickness(0, 0, 5, 0)
                };
                otherButton.Click += (s, e) => _mainWindow.SetLevel3Filter(otherButton.IsChecked == true ? "_OTHER_" : "");
                Level3FilterPanel.Children.Add(otherButton);
            }

            if (_mainWindow.Level3Filters.TryGetValue(category, out var filters))
            {
                foreach (var filter in filters)
                {
                    var l3Button = new ToggleButton
                    {
                        Content = _mainWindow.CurrentLanguage == "zh-CN" ? filter.ChineseName : filter.EnglishName,
                        Tag = filter.EnglishName,
                        Margin = new Thickness(0, 0, 5, 0)
                    };
                    l3Button.Click += (s, e) => _mainWindow.SetLevel3Filter(l3Button.IsChecked == true ? filter.EnglishName : "");
                    Level3FilterPanel.Children.Add(l3Button);
                }
            }

            Level3ScrollViewer.Visibility = Level3FilterPanel.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }


        private void LoadMiniModeConfiguration()
        {
            // This will be expanded to read from config file later.
            // Load configuration from file
            string bgPath = Path.Combine(GetApplicationRoot(), "resource", "zhu1.png");
            if (File.Exists(bgPath))
            {
                try
                {
                    var bgBitmap = new BitmapImage();
                    bgBitmap.BeginInit();
                    bgBitmap.UriSource = new Uri(bgPath);
                    bgBitmap.EndInit();
                    BackgroundImageBrush.ImageSource = bgBitmap;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load mini-mode background: {ex.Message}");
                }
            }
        }

        public class LogFilter : TraceListener
        {
            public override void Write(string? message)
            {
                if (!string.IsNullOrEmpty(message) && !message.Contains("HwndHook received message: 0x90"))
                {
                    Debug.WriteLine(message); // Pass the filtered message to the Debug listeners.
                    Console.Write(message); // Also write to the Console output.
                }
            }

            public override void WriteLine(string? message)
            {
                if (!string.IsNullOrEmpty(message) && !message.Contains("HwndHook received message: 0x90"))
                {
                    Debug.WriteLine(message + Environment.NewLine);
                    Console.WriteLine(message);
                }
            }
        }



        private static string GetApplicationRoot()
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            string? exeDir = Path.GetDirectoryName(exePath);
            return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        private void Gif_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item && item.IsAnimated)
            {
                var carouselGrid = FindVisualChild<Grid>(element, "CarouselGrid");
                var gifPlayer = FindVisualChild<MediaElement>(element, "GifPlayer");

                if (carouselGrid?.TryFindResource("CarouselAnimation") is System.Windows.Media.Animation.Storyboard sb) sb.Pause(carouselGrid);
                if (carouselGrid != null) carouselGrid.Visibility = Visibility.Collapsed;

                if (gifPlayer != null)
                {
                    if (gifPlayer.Source == null) gifPlayer.Source = item.FileUri;
                    gifPlayer.Visibility = Visibility.Visible;
                    gifPlayer.Play();
                }
            }
        }

        private void Gif_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageItem item && item.IsAnimated)
            {
                var gifPlayer = FindVisualChild<MediaElement>(element, "GifPlayer");
                var carouselGrid = FindVisualChild<Grid>(element, "CarouselGrid");

                if (gifPlayer != null)
                {
                    gifPlayer.Stop();
                    gifPlayer.Visibility = Visibility.Collapsed;
                }

                if (carouselGrid != null) carouselGrid.Visibility = Visibility.Visible;
                if (carouselGrid?.TryFindResource("CarouselAnimation") is System.Windows.Media.Animation.Storyboard sb) sb.Resume(carouselGrid);
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
                    System.Windows.Clipboard.SetImage(new BitmapImage(item.FileUri));
                    ShowNotification(PigPicPot.Strings.Resources.ImageCopiedNotification);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"复制失败: {ex.Message}", "Error");
            }
        }

        private void GifPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement me)
            {
                me.Position = TimeSpan.FromMilliseconds(1);
                me.Play();
            }
        }

        private void ShowNotification(string message)
        {
            // 使用Dispatcher确保在UI线程上执行
            Dispatcher.Invoke(() =>
             {
                // 直接使用自动生成的控件字段
                if (NotificationText != null)
                {
                    NotificationText.Text = message;
                }

                if (NotificationOverlay != null)
                {
                    // 尝试查找通知动画资源
                    var storyboard = MainContentPanel.FindResource("NotificationStoryboard") as Storyboard;
                    if (storyboard != null)
                    {
                        storyboard.Completed += (s, e) => { NotificationOverlay.Visibility = Visibility.Collapsed; };
                        NotificationOverlay.Visibility = Visibility.Visible;
                        storyboard.Begin(NotificationOverlay);
                    }
                    else
                    {
                        // 如果找不到动画资源，使用简单的方式显示通知
                         NotificationOverlay.Visibility = Visibility.Visible;
                        NotificationOverlay.Opacity = 1;
                        
                        // 使用Dispatcher延迟隐藏通知
                        var timer = new System.Windows.Threading.DispatcherTimer();
                        timer.Interval = TimeSpan.FromSeconds(2);
                        timer.Tick += (s, e) =>
                        {
                            NotificationOverlay.Opacity = 0;
                            NotificationOverlay.Visibility = Visibility.Collapsed;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                }
            });
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
