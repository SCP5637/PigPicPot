using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PigPicPot
{
    public partial class MainWindow : Window
    {
        private readonly List<ImageItem> _allImageItems = new();
        private readonly Dictionary<ImageItem, MediaElement> _activePlayers = new();
        private ScrollViewer? _scrollViewer;
        private static readonly string ResourcePath = Path.Combine(GetApplicationRoot(), "resource");

        private static string GetApplicationRoot()
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            string? exeDir = Path.GetDirectoryName(exePath);
            return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        public MainWindow()
        {
            LoadConfiguration();
            InitializeComponent();
            Console.WriteLine("MainWindow initialized.");
            LoadImages();
        }

        private void LoadConfiguration()
        {
            try
            {
                string configFile = Path.Combine(GetApplicationRoot(), "config.cfg");
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
";
                    File.WriteAllText(configFile, defaultConfig);
                }

                var config = File.ReadAllLines(configFile).ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                this.Width = 1366;
                this.Height = 768;
            }
        }

        private void LoadImages()
        {
            Console.WriteLine("Starting to load images...");
            _allImageItems.Clear();

            try
            {
                string configFile = Path.Combine(GetApplicationRoot(), "config.cfg");
                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(GetApplicationRoot(), bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        BackgroundImageBrush.ImageSource = new BitmapImage(new Uri(fullBgPath));
                    }
                    else
                    {
                        Console.WriteLine($"Background image not found at: {fullBgPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading background image: {ex.Message}");
            }

            string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var rootFiles = Directory.EnumerateFiles(ResourcePath, "*.*", SearchOption.TopDirectoryOnly).Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
            ProcessFiles(rootFiles);

            string[] subDirs = { @"gif\anime", @"gif\real", @"pic\Other", @"pic\zhuxx" };
            foreach (var dir in subDirs)
            {
                var fullDirPath = Path.Combine(ResourcePath, dir);
                if (Directory.Exists(fullDirPath))
                {
                    var subDirFiles = Directory.EnumerateFiles(fullDirPath, "*.*", SearchOption.AllDirectories).Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
                    ProcessFiles(subDirFiles);
                }
            }
            ApplySortAndFilter();
            Console.WriteLine($"Finished loading. Total items in list: {_allImageItems.Count}");
        }

        private void ProcessFiles(IEnumerable<string> files)
        {
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("zhu3.jpg", StringComparison.OrdinalIgnoreCase)) continue;
                var lastUnderscoreIndex = fileName.LastIndexOf('_');
                var displayFileName = lastUnderscoreIndex != -1 && lastUnderscoreIndex < fileName.Length - 1 ? fileName.Substring(lastUnderscoreIndex + 1) : fileName;
                _allImageItems.Add(new ImageItem
                {
                    FilePath = file, FileUri = new Uri(file), FullFileName = fileName, DisplayFileName = displayFileName,
                    IsGif = Path.GetExtension(file).Equals(".gif", StringComparison.OrdinalIgnoreCase)
                });
            }
        }

        private void ApplySortAndFilter()
        {
            var searchText = SearchTextBox.Text.Trim();
            IEnumerable<ImageItem> filteredList = _allImageItems;
            if (!string.IsNullOrEmpty(searchText)) { filteredList = _allImageItems.Where(item => item.FullFileName.Contains(searchText, StringComparison.OrdinalIgnoreCase)); }
            ImageListBox.ItemsSource = filteredList.OrderBy(item => !item.FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ThenBy(item => item.FullFileName);
            UpdateVisibleGifs();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySortAndFilter();

        private void ImageListBox_Loaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindVisualChild<ScrollViewer>(ImageListBox);
            if (_scrollViewer != null) { _scrollViewer.ScrollChanged += OnScrollChanged; UpdateVisibleGifs(); }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) => UpdateVisibleGifs();

        private void UpdateVisibleGifs()
        {
            if (_scrollViewer == null || !ImageListBox.HasItems) return;
            var keysToRemove = new List<ImageItem>();
            foreach (var entry in _activePlayers) { if (ImageListBox.ItemContainerGenerator.ContainerFromItem(entry.Key) is not ListBoxItem container || !IsUserVisible(container, _scrollViewer)) { entry.Value.Stop(); entry.Value.Source = null; entry.Value.Visibility = Visibility.Collapsed; keysToRemove.Add(entry.Key); } } 
            foreach (var key in keysToRemove) { _activePlayers.Remove(key); }
            for (int i = 0; i < ImageListBox.Items.Count; i++) { if (ImageListBox.Items[i] is ImageItem item && item.IsGif && !_activePlayers.ContainsKey(item)) { if (ImageListBox.ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem container && IsUserVisible(container, _scrollViewer)) { if (FindVisualChild<MediaElement>(container) is MediaElement gifPlayer) { gifPlayer.Source = item.FileUri; gifPlayer.Play(); gifPlayer.Visibility = Visibility.Visible; _activePlayers[item] = gifPlayer; } } } } 
        }

                        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)

                        {

                            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item) return;

                

                            if (item.IsGif)

                            {

                                var border = FindVisualParent<Border>(element);

                                if (border != null && border.ContextMenu != null)

                                {

                                    Console.WriteLine($"GIF clicked, opening ContextMenu for {item.FullFileName}");

                                    border.ContextMenu.DataContext = item; // Pass the specific item to the menu

                                    border.ContextMenu.IsOpen = true;

                                }

                            }

                            else

                            {

                                try 

                                {

                                    Clipboard.SetImage(new BitmapImage(item.FileUri)); 

                                    ShowNotification(Strings.Resources.NotificationCopiedImage);

                                }

                                catch (Exception ex) { MessageBox.Show($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}", "Error"); }

                            }

                        }

                

                        private void CopyStatic_Click(object sender, RoutedEventArgs e)

                        {

                            if (sender is MenuItem menuItem && menuItem.DataContext is ImageItem item)

                            {

                                try 

                                {

                                    Clipboard.SetImage(new BitmapImage(item.FileUri)); 

                                    ShowNotification(Strings.Resources.NotificationCopiedStatic);

                                }

                                catch (Exception ex) { MessageBox.Show($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}", "Error"); }

                            }

                        }

                

                        private void CopyFile_Click(object sender, RoutedEventArgs e)

                        {

                            if (sender is MenuItem menuItem && menuItem.DataContext is ImageItem item)

                            {

                                try 

                                {

                                    Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { item.FilePath }); 

                                    ShowNotification(Strings.Resources.NotificationCopiedFile);

                                }

                                catch (Exception ex) { MessageBox.Show($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}", "Error"); }

                            }

                        }

                

                        private void ShowNotification(string message)

                        {

                            NotificationText.Text = message;

                            if (this.FindResource("NotificationStoryboard") is Storyboard storyboard)

                            {

                                storyboard.Completed += (s, e) => { NotificationOverlay.Visibility = Visibility.Collapsed; };

                                NotificationOverlay.Visibility = Visibility.Visible;

                                storyboard.Begin(NotificationOverlay);

                            }

                        }

                

                        private void GifPlayer_MediaEnded(object sender, RoutedEventArgs e) { if (sender is MediaElement me) { me.Position = TimeSpan.FromMilliseconds(1); me.Play(); } }

                        private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject { if (parent == null) return null; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T t) return t; var childOfChild = FindVisualChild<T>(child); if (childOfChild != null) return childOfChild; } return null; }

                        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject { DependencyObject parentObject = VisualTreeHelper.GetParent(child); if (parentObject == null) return null; if (parentObject is T parent) return parent; return FindVisualParent<T>(parentObject); }

                        private bool IsUserVisible(FrameworkElement element, FrameworkElement container) { if (!element.IsVisible) return false; Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight)); return new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight).IntersectsWith(bounds); }
    }
}
