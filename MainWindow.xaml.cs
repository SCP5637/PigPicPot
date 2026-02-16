using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PigPicPot
{
    public partial class MainWindow : Window
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static readonly Uri RepoTreeApi = new("https://api.github.com/repos/SCP5637/PigPicPot/git/trees/picAssets?recursive=1");
        private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        private static readonly Random Random = new();
        private static readonly SemaphoreSlim CacheLock = new(1, 1);
        private static readonly object CacheSync = new();
        private static List<ImageItem> _cachedItems = new();

        private readonly ObservableCollection<ImageItem> _items = new();

        public ObservableCollection<ImageItem> Items => _items;

        public MainWindow(IEnumerable<ImageItem> items)
        {
            InitializeComponent();
            DataContext = this;
            foreach (var item in items)
            {
                _items.Add(item);
            }
        }

        public static Task PrimeCacheAsync()
        {
            return RefreshCacheAsync();
        }

        public static IReadOnlyList<ImageItem> GetCachedItems()
        {
            lock (CacheSync)
            {
                return _cachedItems.ToList();
            }
        }

        public static async Task RefreshCacheAsync()
        {
            await CacheLock.WaitAsync();
            try
            {
                var tree = await LoadRepoTreeAsync();
                var files = tree
                    .Where(item => item.Type == "blob" && ImageExtensions.Contains(Path.GetExtension(item.Path).ToLowerInvariant()))
                    .ToList();

                var gifs = files.Where(item => Path.GetExtension(item.Path).Equals(".gif", StringComparison.OrdinalIgnoreCase)).ToList();
                var statics = files.Where(item => !Path.GetExtension(item.Path).Equals(".gif", StringComparison.OrdinalIgnoreCase)).ToList();

                var chosen = ChooseRandomItems(gifs, statics, 27);
                var downloadedItems = await DownloadFilesAsync(chosen);
                
                if (downloadedItems.Count > 0)
                {
                    lock (CacheSync)
                    {
                        _cachedItems = downloadedItems;
                    }
                }
            }
            catch
            {
            }
            finally
            {
                CacheLock.Release();
            }
        }

        private bool _isClosing = false;
        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (_isClosing) return;
            try
            {
                _isClosing = true;
                Close();
            }
            catch
            {
                // Ignore closing errors
            }
        }

        public static List<ImageItem> TakeCachedItems(int count)
        {
            lock (CacheSync)
            {
                if (_cachedItems.Count == 0)
                {
                    return new List<ImageItem>();
                }

                var selected = new List<ImageItem>();
                while (selected.Count < count && _cachedItems.Count > 0)
                {
                    var index = Random.Next(_cachedItems.Count);
                    selected.Add(_cachedItems[index]);
                    _cachedItems.RemoveAt(index);
                }

                return selected;
            }
        }

        private static List<GitTreeItem> ChooseRandomItems(List<GitTreeItem> gifs, List<GitTreeItem> statics, int count)
        {
            var chosen = new List<GitTreeItem>();

            if (gifs.Count > 0)
            {
                chosen.Add(gifs[Random.Next(gifs.Count)]);
            }

            if (statics.Count > 0)
            {
                chosen.Add(statics[Random.Next(statics.Count)]);
            }

            var combined = gifs.Concat(statics).Distinct().ToList();
            while (chosen.Count < count && combined.Count > 0)
            {
                var pick = combined[Random.Next(combined.Count)];
                if (!chosen.Contains(pick))
                {
                    chosen.Add(pick);
                }
            }

            return chosen.Take(count).ToList();
        }

        private static string EnsureTempFile(ImageItem item)
        {
            if (item.TempPath != null && File.Exists(item.TempPath))
            {
                return item.TempPath;
            }

            if (item.Data == null)
            {
                throw new InvalidOperationException("Image data is missing.");
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "PigPicPot", "cache");
            Directory.CreateDirectory(tempDir);
            var extension = item.IsAnimated ? ".gif" : ".tmp";
            var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}{extension}");
            File.WriteAllBytes(tempPath, item.Data);
            item.TempPath = tempPath;
            return tempPath;
        }

        private static async Task<List<ImageItem>> DownloadFilesAsync(List<GitTreeItem> items)
        {
            var tasks = items.Select(DownloadFileAsync);
            var results = await Task.WhenAll(tasks);
            return results.Where(result => result != null).Select(result => result!).ToList();
        }

        private static async Task<ImageItem?> DownloadFileAsync(GitTreeItem item)
        {
            try
            {
                var extension = Path.GetExtension(item.Path);
                var rawUrl = $"https://raw.githubusercontent.com/SCP5637/PigPicPot/picAssets/{item.Path}";
                
                byte[] data;
                using (var response = await HttpClient.GetAsync(rawUrl))
                {
                    response.EnsureSuccessStatusCode();
                    data = await response.Content.ReadAsByteArrayAsync();
                }

                if (data.Length == 0) return null;

                var bitmap = new BitmapImage();
                using (var stream = new MemoryStream(data))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();

                return new ImageItem
                {
                    Data = data,
                    Thumbnail = bitmap,
                    IsAnimated = extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                };
            }
            catch
            {
                return null;
            }
        }

        private static async Task<List<GitTreeItem>> LoadRepoTreeAsync()
        {
            using var response = await HttpClient.GetAsync(RepoTreeApi);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var tree = await JsonSerializer.DeserializeAsync<GitTreeResponse>(stream, options);
            return tree?.Tree ?? new List<GitTreeItem>();
        }

        private void Gif_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item || !item.IsAnimated) return;

            var player = FindChild<MediaElement>(element);
            var image = FindChild<System.Windows.Controls.Image>(element);
            if (player == null || image == null) return;

            try
            {
                var tempPath = EnsureTempFile(item);
                player.Source = new Uri(tempPath);
                player.Visibility = Visibility.Visible;
                image.Visibility = Visibility.Collapsed;
                player.Play();
            }
            catch
            {
                // Ignore error
            }
        }

        private void Gif_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item || !item.IsAnimated) return;

            var player = FindChild<MediaElement>(element);
            var image = FindChild<System.Windows.Controls.Image>(element);
            if (player == null || image == null) return;

            player.Stop();
            player.Visibility = Visibility.Collapsed;
            image.Visibility = Visibility.Visible;
        }

        private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element || element.DataContext is not ImageItem item) return;

            try
            {
                if (item.IsAnimated)
                {
                    var tempPath = EnsureTempFile(item);
                    ClipboardHelper.SetAnimatedGif(tempPath);
                }
                else
                {
                    System.Windows.Clipboard.SetImage(item.Thumbnail);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"复制失败: {ex.Message}", "Error");
            }

            Close();
        }

        private void GifPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (sender is MediaElement player)
            {
                player.Position = TimeSpan.FromMilliseconds(1);
                player.Play();
            }
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }
                var result = FindChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PigPicPot");
            return client;
        }

        private class GitTreeResponse
        {
            public List<GitTreeItem> Tree { get; set; } = new();
        }

        private class GitTreeItem
        {
            public string Path { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
        }
    }
}
