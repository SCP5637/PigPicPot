using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Forms;
using System.Windows.Media.Animation;

namespace PigPicPot
{

    public class ProgressReport
    {
        public int FilesProcessed { get; set; }
        public int TotalFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }

    public partial class MainWindow : Window
    {
        private readonly List<ImageItem> _allImageItems = new();
        private readonly Dictionary<string, List<L3FilterInfo>> _level3Filters = new();
        public IEnumerable<ImageItem> AllImageItems => _allImageItems;
        private string _activeMainFilter = "";
        private string _activeSubFilter = "";
        private string _activeLevel3Filter = "";
        private string _language = "en"; // Default to English
        private HotkeyHelper? _hotkeyHelper;
        private MiniModeWindow? _miniModeWindow;


        public class L3FilterInfo
        {
            public required string ChineseName { get; set; }
            public required string EnglishName { get; set; }
        }

        public MainWindow()
        {
            LoadConfiguration();
            InitializeComponent();
            Console.WriteLine("MainWindow initialized.");
            this.Closed += MainWindow_Closed;
            LoadImages();
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
                string configFile = Path.Combine(GetApplicationRoot(), "config.cfg");
                Console.WriteLine($"Reading config from: {configFile}");
                Console.WriteLine($"Config file exists: {File.Exists(configFile)}");
                
                if (!File.Exists(configFile))
                {
                    // 如果应用程序目录没有配置文件，尝试项目目录
                    string projectConfigFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "config.cfg");
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

        private void ToggleMiniMode()
        {
            Console.WriteLine("--- Hotkey pressed! ToggleMiniMode() was called. ---");
            if (_miniModeWindow == null)
            {
                _miniModeWindow = new MiniModeWindow(this);
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

                if (config.TryGetValue("language", out var langValue))
                {
                    _language = langValue;
                }

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

        private static readonly System.Text.RegularExpressions.Regex _fileNameRegex = new System.Text.RegularExpressions.Regex(@"^pig_(.+?)_([^_]+?)(\d*)$", System.Text.RegularExpressions.RegexOptions.Compiled);

        private void DiscoverFilterGroups()
        {
            _level3Filters.Clear();
            var itemsByParentDir = _allImageItems.GroupBy(item => new DirectoryInfo(item.FilePath).Parent?.Name);

            foreach (var group in itemsByParentDir)
            {
                if (group.Key == null) continue;

                var parentDir = group.Key;
                var baseNameCounts = group
                    .Where(item => !string.IsNullOrEmpty(item.BaseChineseName))
                    .GroupBy(item => item.BaseChineseName)
                    .ToDictionary(g => g.Key, g => g.Count());

                foreach (var item in group)
                {
                    if (!string.IsNullOrEmpty(item.BaseChineseName) && baseNameCounts.TryGetValue(item.BaseChineseName, out int count) && count == 1)
                    {
                        item.IsSingleton = true;
                    }
                }

                var seriesFilters = baseNameCounts
                    .Where(kvp => kvp.Value > 1)
                    .Select(kvp => group.First(item => item.BaseChineseName == kvp.Key))
                    .Select(item => new L3FilterInfo { ChineseName = item.BaseChineseName, EnglishName = item.BaseEnglishName })
                    .OrderBy(f => f.ChineseName)
                    .ToList();

                if (seriesFilters.Any())
                {
                    _level3Filters[parentDir] = seriesFilters;
                }
            }
        }


        private async void LoadImages()
        {
            SetControlsEnabled(false);

            Console.WriteLine("Starting to load images...");
            _allImageItems.Clear();
            _level3Filters.Clear();

            // Background Image Loading...
            try
            {
                string configFile = Path.Combine(GetApplicationRoot(), "config.cfg");
                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(GetApplicationRoot(), bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        BackgroundImageBrush.ImageSource = bgBitmap;

                        const string ExpectedHash = "0628b9d8d23d7a695938425fef17f9da4643246f4e410ab44461bdae8349a303";
                        string currentHash = ComputeFileHash(fullBgPath);
                        if (Path.GetFileName(fullBgPath).Equals("zhu3.jpg", StringComparison.OrdinalIgnoreCase) &&
                            bgBitmap.PixelWidth == 1920 && bgBitmap.PixelHeight == 1176 &&
                            currentHash.Equals(ExpectedHash, StringComparison.OrdinalIgnoreCase))
                        {
                            InfoButton.Visibility = Visibility.Visible;
                            Console.WriteLine("Special background detected. Info button enabled.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading background image: {ex.Message}");
            }

            // 1. Get a complete list of files to process.
            var filesToProcess = new List<string>();
            string[] extensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            string[] subDirs = { @"gif/anime", @"gif/real", @"pic/Other", @"pic/zhuxx" };
            foreach (var dir in subDirs)
            {
                var fullDirPath = Path.Combine(GetApplicationRoot(), "resource", dir.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(fullDirPath))
                {
                    filesToProcess.AddRange(Directory.EnumerateFiles(fullDirPath, "*.*", SearchOption.AllDirectories).Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant())));
                }
            }

            int totalFiles = filesToProcess.Count;
            LoadingProgressBar.Maximum = totalFiles;

            // 2. Setup progress reporting
            var progress = new Progress<ProgressReport>(report =>
            {
                LoadingProgressBar.Value = report.FilesProcessed;
                LoadingProgressText.Text = $"正在加载 ({report.FilesProcessed}/{report.TotalFiles}): {report.CurrentFile}";
            });

            // 3. Asynchronously process all images
            int failedFiles = await Task.Run(() => ProcessFiles(filesToProcess, progress));

            // 4. Discover filter groups from the loaded data
            DiscoverFilterGroups();

            // 5. Finalize UI
            LoadingOverlay.Visibility = Visibility.Collapsed;
            MainContentPanel.Visibility = Visibility.Visible;
            SetControlsEnabled(true);

            int totalAttempted = filesToProcess.Count;
            int successfulFiles = _allImageItems.Count;
            int dynamicImages = _allImageItems.Count(item => item.IsAnimated);
            int staticImages = successfulFiles - dynamicImages;
            LoadSummaryTextBlock.Text = $"本次加载了{totalAttempted}个图片，其中{successfulFiles}个成功，{failedFiles}个失败，{dynamicImages}个动态图片，{staticImages}个静态图片。";

            ImageListBox.ItemsSource = null; // Do not show any images on startup
            SummaryTextBlock.Text = "请输入关键词或选择分类以开始";
            Console.WriteLine($"Finished loading. Total items in list: {_allImageItems.Count}");
        }

        private void SetControlsEnabled(bool isEnabled)
        {
            SearchTextBox.IsEnabled = isEnabled;
            StaticFilterButton.IsEnabled = isEnabled;
            DynamicFilterButton.IsEnabled = isEnabled;
            StaticSubFilterPanel.IsEnabled = isEnabled;
            DynamicSubFilterPanel.IsEnabled = isEnabled;
            Level3FilterPanel.IsEnabled = isEnabled;
        }

        private int ProcessFiles(List<string> files, IProgress<ProgressReport> progress)
        {
            int filesProcessed = 0;
            int totalFiles = files.Count;
            int failedCount = 0;

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                filesProcessed++;

                try
                {
                    progress.Report(new ProgressReport { FilesProcessed = filesProcessed, TotalFiles = totalFiles, CurrentFile = fileName });
                    Console.WriteLine($"Processing ({filesProcessed}/{totalFiles}): {fileName}");

                    if (fileName.Equals("zhu3.jpg", StringComparison.OrdinalIgnoreCase)) continue;

                    // --- Start of Refactored Logic ---
                    // 1. Parse all name variations first.
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                    string baseChineseName = "";
                    string baseEnglishName = "";
                    string fullChineseName = "";
                    string fullEnglishName = "";

                    if (nameWithoutExt.StartsWith("pig_"))
                    {
                        string name = nameWithoutExt.Substring(4); // Remove "pig_"
                        int firstChineseIndex = -1;
                        for (int i = 0; i < name.Length; i++) { if (name[i] >= 0x4E00 && name[i] <= 0x9FFF) { firstChineseIndex = i; break; } }

                        if (firstChineseIndex != -1)
                        {
                            fullEnglishName = name.Substring(0, firstChineseIndex).TrimEnd('_');
                            fullChineseName = name.Substring(firstChineseIndex);
                            baseEnglishName = System.Text.RegularExpressions.Regex.Replace(fullEnglishName, @"[\d_-]+$", "").TrimEnd('_');
                            baseChineseName = System.Text.RegularExpressions.Regex.Replace(fullChineseName, @"[\d_-]+$", "").TrimEnd('_');
                        }
                        else
                        {
                            fullEnglishName = name;
                            baseEnglishName = System.Text.RegularExpressions.Regex.Replace(name, @"[\d_-]+$", "").TrimEnd('_');
                        }
                    }

                    // 2. Construct the display name based on language.
                    string extension = Path.GetExtension(fileName);
                    string displayFileName;
                    if (_language == "zh-CN" && !string.IsNullOrEmpty(fullChineseName))
                    {
                        displayFileName = fullChineseName + extension;
                    }
                    else
                    {
                        displayFileName = ("pig_" + fullEnglishName).Replace('_', ' ') + extension;
                    }

                    // 3. Now create the ImageItem with all required properties.
                    var imageItem = new ImageItem
                    {
                        FilePath = file,
                        FileUri = new Uri(file),
                        FullFileName = fileName,
                        DisplayFileName = displayFileName, // Set required member here
                        IsAnimated = Path.GetExtension(file).Equals(".gif", StringComparison.OrdinalIgnoreCase),
                        BaseChineseName = baseChineseName,
                        BaseEnglishName = baseEnglishName,
                        FullChineseName = fullChineseName,
                        FullEnglishName = fullEnglishName
                    };
                    // --- End of Refactored Logic ---

                    if (imageItem.IsAnimated)
                    {
                        using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(file))
                        {
                            if (image.Frames.Count > 0)
                            {
                                imageItem.StartFrame = ConvertToBitmapSource(image.Frames.CloneFrame(0));
                                imageItem.MiddleFrame = ConvertToBitmapSource(image.Frames.CloneFrame(image.Frames.Count / 2));
                                imageItem.EndFrame = ConvertToBitmapSource(image.Frames.CloneFrame(image.Frames.Count - 1));
                            }
                        }
                    }
                    else
                    {
                        var bitmap = new BitmapImage();
                        using (var stream = new FileStream(imageItem.FilePath, FileMode.Open, FileAccess.Read))
                        {
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.StreamSource = stream;
                            bitmap.EndInit();
                        }
                        bitmap.Freeze();
                        imageItem.StartFrame = bitmap;
                        imageItem.MiddleFrame = bitmap;
                        imageItem.EndFrame = bitmap;
                    }

                    _allImageItems.Add(imageItem);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"---!!! FAILED to load image {fileName}: {ex.Message}");
                    failedCount++;
                }
            }
            return failedCount;
        }

        private static BitmapSource ConvertToBitmapSource(Image<Rgba32> image)
        {
            using (var memoryStream = new MemoryStream())
            {
                image.SaveAsBmp(memoryStream);
                memoryStream.Position = 0;

                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memoryStream;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze(); // Important for performance and cross-thread access
                return bitmapImage;
            }
        }

        public event Action<IEnumerable<ImageItem>>? FilterChanged;
        public string CurrentSearchText => SearchTextBox.Text;
        public string ActiveMainFilter => _activeMainFilter;
        public string ActiveSubFilter => _activeSubFilter;
        public string ActiveLevel3Filter => _activeLevel3Filter;
        public Dictionary<string, List<L3FilterInfo>> Level3Filters => _level3Filters;
        public string CurrentLanguage => _language;

        public void SetSearchText(string text)
        {
            SearchTextBox.Text = text;
        }

        public void SetMainFilter(string filter)
        {
            if (filter == "Static")
            {
                StaticFilterButton.IsChecked = true;
                StaticFilterButton_Click(StaticFilterButton, new RoutedEventArgs());
            }
            else if (filter == "Dynamic")
            {
                DynamicFilterButton.IsChecked = true;
                DynamicFilterButton_Click(DynamicFilterButton, new RoutedEventArgs());
            }
            else
            {
                StaticFilterButton.IsChecked = false;
                DynamicFilterButton.IsChecked = false;
                _activeMainFilter = "";
                ApplySortAndFilter();
            }
        }

        public void SetSubFilter(string filter)
        {
            var panel = _activeMainFilter == "Static" ? StaticSubFilterPanel : DynamicSubFilterPanel;
            foreach (System.Windows.Controls.Primitives.ToggleButton btn in panel.Children)
            {
                bool shouldBeChecked = (btn.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
                if (btn.IsChecked != shouldBeChecked)
                {
                    btn.IsChecked = shouldBeChecked;
                    SubFilter_Click(btn, new RoutedEventArgs());
                }
            }
        }

        public void SetLevel3Filter(string filter)
        {
            foreach (System.Windows.Controls.Primitives.ToggleButton btn in Level3FilterPanel.Children)
            {
                bool shouldBeChecked = (btn.Tag as string) == filter;
                if (btn.IsChecked != shouldBeChecked)
                {
                    btn.IsChecked = shouldBeChecked;
                    Level3Filter_Click(btn, new RoutedEventArgs());
                }
            }
        }

        private void ApplySortAndFilter()
        {
            // If no filters are active, show nothing.
            bool noFiltersActive = string.IsNullOrEmpty(_activeMainFilter) &&
                                   string.IsNullOrEmpty(_activeSubFilter) &&
                                   string.IsNullOrEmpty(_activeLevel3Filter) &&
                                   string.IsNullOrEmpty(SearchTextBox.Text);

            if (noFiltersActive)
            {
                ImageListBox.ItemsSource = null; // Clear the view
                SummaryTextBlock.Text = "请输入关键词或选择分类以开始";
                FilterChanged?.Invoke(new List<ImageItem>());
                return; // Exit early
            }

            var searchText = SearchTextBox.Text.Trim();
            IEnumerable<ImageItem> filteredList = _allImageItems;

            // 1. Main category filter
            if (_activeMainFilter == "Static")
            {
                filteredList = filteredList.Where(item => !item.IsAnimated);
            }
            else if (_activeMainFilter == "Dynamic")
            {
                filteredList = filteredList.Where(item => item.IsAnimated);
            }

            // 2. Sub-category filter (by folder path)
            if (!string.IsNullOrEmpty(_activeSubFilter))
            {
                string filterPath = "\\" + _activeSubFilter + "\\";
                filteredList = filteredList.Where(item => item.FilePath.Contains(filterPath, StringComparison.OrdinalIgnoreCase));
            }

            // 3. Level 3 filter (by base name)
            if (!string.IsNullOrEmpty(_activeLevel3Filter))
            {
                if (_activeLevel3Filter == "_OTHER_")
                {
                    filteredList = filteredList.Where(item => item.IsSingleton);
                }
                else
                {
                    filteredList = filteredList.Where(item => item.BaseEnglishName.Equals(_activeLevel3Filter, StringComparison.OrdinalIgnoreCase));
                }
            }

            // 4. Search text filter
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredList = filteredList.Where(item => item.FullFileName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var resultList = filteredList.OrderBy(item => !item.FilePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ThenBy(item => item.FullFileName).ToList();
            ImageListBox.ItemsSource = resultList;

            int totalCount = _allImageItems.Count;
            int filteredCount = resultList.Count;
            SummaryTextBlock.Text = $"共 {filteredCount} 个结果 / 总计 {totalCount} 个";
            FilterChanged?.Invoke(resultList);
        }

        private void StaticFilterButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = StaticFilterButton.IsChecked == true;

            if (isChecked)
            {
                _activeMainFilter = "Static";
                DynamicFilterButton.IsChecked = false;
                _activeSubFilter = "";
                _activeLevel3Filter = "";
                ResetSubFilters(false); 
            }
            else
            {
                _activeMainFilter = "";
            }

            StaticSubFilterPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            DynamicSubFilterPanel.Visibility = Visibility.Collapsed;
            Level3FilterPanel.Visibility = Visibility.Collapsed;

            ApplySortAndFilter();
        }

        private void DynamicFilterButton_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = DynamicFilterButton.IsChecked == true;
            
            if (isChecked)
            {
                _activeMainFilter = "Dynamic";
                StaticFilterButton.IsChecked = false;
                _activeSubFilter = "";
                _activeLevel3Filter = "";
                ResetSubFilters(true);
            }
            else
            {
                _activeMainFilter = "";
            }

            DynamicSubFilterPanel.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            StaticSubFilterPanel.Visibility = Visibility.Collapsed;
            Level3FilterPanel.Visibility = Visibility.Collapsed;

            ApplySortAndFilter();
        }

        private void SubFilter_Click(object sender, RoutedEventArgs e)
        { 
            var clickedButton = (System.Windows.Controls.Primitives.ToggleButton)sender;
            string filterName = "";

            if (clickedButton.IsChecked == true)
            {
                var panel = StaticSubFilterPanel.Children.Contains(clickedButton) ? StaticSubFilterPanel : DynamicSubFilterPanel;
                foreach (System.Windows.Controls.Primitives.ToggleButton btn in panel.Children)
                {
                    if (btn != clickedButton) btn.IsChecked = false;
                }

                if (clickedButton == ZhuxxFilterButton) filterName = "zhuxx";
                else if (clickedButton == OtherFilterButton) filterName = "Other";
                else if (clickedButton == AnimeFilterButton) filterName = "anime";
                else if (clickedButton == RealFilterButton) filterName = "real";
                
                _activeSubFilter = filterName;
                _activeLevel3Filter = ""; // Reset L3 filter when L2 changes
                PopulateLevel3Filters(filterName);
            }
            else
            {
                _activeSubFilter = "";
                _activeLevel3Filter = "";
                Level3FilterPanel.Visibility = Visibility.Collapsed;
            }

            ApplySortAndFilter();
        }

        private void PopulateLevel3Filters(string category)
        {
            Level3FilterPanel.Children.Clear();
            Level3FilterPanel.Visibility = Visibility.Collapsed;

            bool hasSingletons = _allImageItems.Any(i => i.FilePath.Contains("\\" + category + "\\") && i.IsSingleton);

            if (hasSingletons)
            {
                var otherButton = new System.Windows.Controls.Primitives.ToggleButton
                {
                    Content = _language == "zh-CN" ? "其他" : "Other",
                    Tag = "_OTHER_",
                    Margin = new Thickness(0, 0, 5, 5)
                };
                otherButton.Click += Level3Filter_Click;
                Level3FilterPanel.Children.Add(otherButton);
            }

            if (_level3Filters.TryGetValue(category, out var filters))
            {
                foreach (var filter in filters)
                {
                    var l3Button = new System.Windows.Controls.Primitives.ToggleButton
                    {
                        Content = _language == "zh-CN" ? filter.ChineseName : filter.EnglishName,
                        Tag = filter.EnglishName,
                        Margin = new Thickness(0, 0, 5, 5)
                    };
                    l3Button.Click += Level3Filter_Click;
                    Level3FilterPanel.Children.Add(l3Button);
                }
            }

            if (Level3FilterPanel.Children.Count > 0)
            {
                Level3FilterPanel.Visibility = Visibility.Visible;
            }
        }

        private void Level3Filter_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = (System.Windows.Controls.Primitives.ToggleButton)sender;
            if (clickedButton.IsChecked == true)
            {
                foreach (System.Windows.Controls.Primitives.ToggleButton btn in Level3FilterPanel.Children)
                {
                    if (btn != clickedButton) btn.IsChecked = false;
                }
                _activeLevel3Filter = clickedButton.Tag as string ?? "";
            }
            else
            {
                _activeLevel3Filter = "";
            }
            ApplySortAndFilter();
        }

        private void ResetSubFilters(bool isStatic)
        {
            var panel = isStatic ? StaticSubFilterPanel : DynamicSubFilterPanel;
            foreach (System.Windows.Controls.Primitives.ToggleButton btn in panel.Children)
            {
                btn.IsChecked = false;
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySortAndFilter();
        private void Image_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item) return;

            try
            {
                if (item.IsAnimated)
                {
                    ClipboardHelper.SetAnimatedGif(item.FilePath);
                    ShowNotification(Strings.Resources.NotificationCopiedImage);
                }
                else
                {
                    System.Windows.Clipboard.SetImage(new BitmapImage(item.FileUri));
                    ShowNotification(Strings.Resources.NotificationCopiedImage);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}", "Error");
            }
        }

        private void Image_MouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item) return;

            var contextMenu = new ContextMenu();
            var copyFileMenuItem = new MenuItem { Header = Strings.Resources.ContextMenuCopyFile };
            copyFileMenuItem.Click += (s, args) => {
                if (item != null)
                {
                    try
                    {
                        System.Windows.Clipboard.SetFileDropList(new System.Collections.Specialized.StringCollection { item.FilePath });
                        ShowNotification(Strings.Resources.NotificationCopiedFile);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"{Strings.Resources.ErrorFailedToCopy} {ex.Message}", "Error");
                    }
                }
            };
            copyFileMenuItem.DataContext = item;

            var exportFileMenuItem = new MenuItem { Header = Strings.Resources.ContextMenuExportTo };
            exportFileMenuItem.Click += (s, args) => ExportFile_Click(s, args);
            exportFileMenuItem.DataContext = item;

            contextMenu.Items.Add(copyFileMenuItem);
            contextMenu.Items.Add(exportFileMenuItem);

            element.ContextMenu = contextMenu;
            contextMenu.IsOpen = true;
        }

        private void ExportFile_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.DataContext is not ImageItem item) return;

            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    string destPath = Path.Combine(dialog.SelectedPath, item.FullFileName);
                    File.Copy(item.FilePath, destPath, true);
                    ShowNotification($"{Strings.Resources.NotificationExportedTo} {dialog.SelectedPath}");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"{Strings.Resources.ErrorFailedToExport} {ex.Message}", "Error");
                }
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

        private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject { DependencyObject parentObject = VisualTreeHelper.GetParent(child); if (parentObject == null) return null; if (parentObject is T parent) return parent; return FindVisualParent<T>(parentObject); }
        private bool IsUserVisible(FrameworkElement element, FrameworkElement container) { if (!element.IsVisible) return false; Rect bounds = element.TransformToAncestor(container).TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight)); return new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight).IntersectsWith(bounds); }
        private static string GetApplicationRoot()
        {
            string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            string? exeDir = Path.GetDirectoryName(exePath);
            return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
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
            MainContentPanel.Visibility = MainContentPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
