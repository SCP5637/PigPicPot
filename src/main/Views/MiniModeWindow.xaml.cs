using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public MiniModeWindow()
        {
            InitializeComponent();
            LoadMiniModeConfiguration();
            
            // 创建并设置ViewModel
            var tagProvider = new TagProvider();
            var thumbnailService = new ThumbnailService();
            var settingsService = new SettingsService();
            var messenger = new Messenger();
            
            var viewModel = new MiniViewModel(tagProvider, thumbnailService, settingsService, messenger, Dispatcher);
            DataContext = viewModel;

            // 订阅消息
            messenger.Register<CloseMiniWindowMessage>(this, (recipient, message) =>
            {
                this.Close();
            });
            messenger.Register<ShowNotificationMessage>(this, OnNotificationReceived);
        }

        private void LoadMiniModeConfiguration()
        {
            try
            {
                string configFile = Path.Combine(PathHelper.GetApplicationRoot(), "usersettings.json");
                if (!File.Exists(configFile)) return;

                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                // Set window size
                this.Width = config.TryGetValue("mini_mode_width", out var w) && int.TryParse(w, out int width) ? width : 640;
                this.Height = config.TryGetValue("mini_mode_height", out var h) && int.TryParse(h, out int height) ? height : 480;

                // Set background image
                if (config.TryGetValue("mini_mode_background", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathHelper.GetApplicationRoot(), bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        
                        var backgroundBrush = FindName("BackgroundImageBrush") as ImageBrush;
                        if (backgroundBrush != null)
                        {
                            backgroundBrush.ImageSource = bgBitmap;
                        }

                        // Check for secret button
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

        public class LogFilter : TraceListener
        {
            private static readonly Regex _hwndHookRegex = new Regex(@"HwndHook received message: 0x(90|3|7C|7D)", RegexOptions.Compiled);

            public override void Write(string? message)
            {
                if (!string.IsNullOrEmpty(message) && !_hwndHookRegex.IsMatch(message))
                {
                    Debug.WriteLine(message); // Pass the filtered message to the Debug listeners.
                    Console.Write(message); // Also write to the Console output.
                }
            }

            public override void WriteLine(string? message)
            {
                if (!string.IsNullOrEmpty(message) && !_hwndHookRegex.IsMatch(message))
                {
                    Debug.WriteLine(message + Environment.NewLine);
                    Console.WriteLine(message);
                }
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



        private void OnNotificationReceived(object sender, ShowNotificationMessage message)
        {
            Dispatcher.Invoke(() =>
            {
                ShowNotification(message.Text);
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
                    var thumbnailService = (DataContext as MiniViewModel)?.ThumbnailService;
                    thumbnailService?.QueueThumbnailRequest(item);
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