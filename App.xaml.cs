using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PigPicPot
{
    public partial class App : System.Windows.Application
    {
        private Forms.NotifyIcon? _trayIcon;
        private HotkeyHelper? _hotkeyHelper;
        private Window? _hotkeyHost;
        private MainWindow? _activeWindow;
        private Task _cacheTask = Task.CompletedTask;

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            FreeConsole();

            try
            {
                _hotkeyHost = new Window
                {
                    Width = 1,
                    Height = 1,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Opacity = 0,
                    Left = -10000,
                    Top = -10000
                };
                _hotkeyHost.ShowActivated = false;
                _hotkeyHost.Show();
                _hotkeyHost.Hide();
                MainWindow = _hotkeyHost;

                _trayIcon = new Forms.NotifyIcon();
                using var iconStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PigPicPot.icon.ico");
                if (iconStream != null)
                {
                    _trayIcon.Icon = new System.Drawing.Icon(iconStream);
                }
                else
                {
                    _trayIcon.Icon = System.Drawing.SystemIcons.Application;
                }
                _trayIcon.Visible = true;
                _trayIcon.Text = "PigPicPot";
                _trayIcon.DoubleClick += (s, args) => HandleHotkey();

                var menu = new Forms.ContextMenuStrip();
                var exitItem = new Forms.ToolStripMenuItem("退出程序");
                exitItem.Click += (s, args) => Shutdown();
                menu.Items.Add(exitItem);
                _trayIcon.ContextMenuStrip = menu;

                _hotkeyHelper = new HotkeyHelper(_hotkeyHost);
                if (!_hotkeyHelper.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.B, HandleHotkey))
                {
                     _trayIcon.ShowBalloonTip(3000, "热键注册失败", "Ctrl+Alt+B 被占用或注册失败。程序仍可运行，请尝试通过托盘图标操作。", Forms.ToolTipIcon.Warning);
                }

                _cacheTask = PigPicPot.MainWindow.PrimeCacheAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "Error");
                Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyHelper?.Dispose();
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Windows.MessageBox.Show($"未处理异常: {e.Exception.Message}", "Error");
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            var message = exception?.Message ?? "Unknown error";
            if (Current?.Dispatcher != null)
            {
                Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"致命异常: {message}", "Error");
                });
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (Current?.Dispatcher != null)
            {
                Current.Dispatcher.Invoke(() =>
                {
                    System.Windows.MessageBox.Show($"后台任务异常: {e.Exception.InnerException?.Message ?? e.Exception.Message}", "Error");
                });
            }
            e.SetObserved();
        }

        private static void PositionWindowNearCursor(Window window)
        {
            var cursor = System.Windows.Forms.Control.MousePosition;
            var workArea = SystemParameters.WorkArea;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = cursor.X - window.Width / 2;
            window.Top = cursor.Y - window.Height / 2;

            if (window.Left < workArea.Left) window.Left = workArea.Left;
            if (window.Top < workArea.Top) window.Top = workArea.Top;
            if (window.Left + window.Width > workArea.Right) window.Left = workArea.Right - window.Width;
            if (window.Top + window.Height > workArea.Bottom) window.Top = workArea.Bottom - window.Height;
        }

        private async void HandleHotkey()
        {
            if (_activeWindow != null)
            {
                try
                {
                    if (_activeWindow.IsLoaded && _activeWindow.IsVisible)
                    {
                        _activeWindow.Activate();
                        return;
                    }
                }
                catch
                {
                    // Ignore errors if window is closing
                }
                _activeWindow = null;
            }

            try
            {
                if (_cacheTask.IsFaulted || _cacheTask.IsCanceled)
                {
                    _cacheTask = PigPicPot.MainWindow.RefreshCacheAsync();
                }

                var cachedCount = PigPicPot.MainWindow.GetCachedItems().Count;
                if (cachedCount < 9)
                {
                    if (!_cacheTask.IsCompleted)
                    {
                        try
                        {
                            await _cacheTask;
                        }
                        catch
                        {
                        }
                    }

                    cachedCount = PigPicPot.MainWindow.GetCachedItems().Count;
                    if (cachedCount < 9)
                    {
                        _cacheTask = PigPicPot.MainWindow.RefreshCacheAsync();
                        try
                        {
                            await _cacheTask;
                        }
                        catch
                        {
                        }
                    }
                }

                var items = PigPicPot.MainWindow.TakeCachedItems(9);
                if (items.Count == 0)
                {
                    throw new Exception("No images available. Please check your internet connection.");
                }

                _activeWindow = new MainWindow(items);
                _activeWindow.Closing += (s, args) => _activeWindow = null;
                _activeWindow.Closed += (s, args) => _activeWindow = null;
                PositionWindowNearCursor(_activeWindow);
                _activeWindow.Show();
                try
                {
                    _activeWindow.Activate();
                }
                catch
                {
                }

                // Trigger cache refresh for next time
                _cacheTask = PigPicPot.MainWindow.RefreshCacheAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"打开窗口失败: {ex.Message}", "Error");
                _activeWindow = null;
            }
        }
    }
}
