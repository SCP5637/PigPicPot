using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Views;
using Application = System.Windows.Application;

namespace PigPicPot.Services
{
    /// <summary>
    /// 托盘服务，负责管理系统托盘图标和相关功能
    /// Tray service, responsible for managing system tray icon and related functions
    /// </summary>
    public class TrayService
    {
        private NotifyIcon? _notifyIcon;
        private readonly MainWindow _mainWindow;
        private static int _trayNotificationCount = 0;
        private const int MaxTrayNotifications = 2;

        /// <summary>
        /// 构造函数
        /// Constructor
        /// </summary>
        /// <param name="mainWindow">主窗口引用</param>
        public TrayService(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        /// <summary>
        /// 初始化托盘图标
        /// Initialize tray icon
        /// </summary>
        public void InitializeTrayIcon()
        {
            LoggingHelper.Log("Initializing tray icon...");
            try
            {
                _notifyIcon = new NotifyIcon();
                
                // 使用项目中的图标文件
                string iconPath = Path.Combine(PathManager.AppRoot, "icon.ico");
                if (File.Exists(iconPath))
                {
                    _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
                    LoggingHelper.Log("Using custom icon for tray.");
                }
                else
                {
                    // 如果项目中的图标文件不存在，则使用应用程序图标
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                        System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "PigPicPot.exe");
                    LoggingHelper.Log("Using application icon for tray.");
                }

                _notifyIcon.Visible = true;
                _notifyIcon.Text = "PigPicPot";

                // 创建右键菜单
                var contextMenu = new ContextMenuStrip();

                var openMenuItem = new ToolStripMenuItem("打开主窗口");
                openMenuItem.Click += (sender, e) => ShowMainWindow();
                contextMenu.Items.Add(openMenuItem);

                var exitMenuItem = new ToolStripMenuItem("退出程序");
                exitMenuItem.Click += (sender, e) => ExitApplication();
                contextMenu.Items.Add(exitMenuItem);

                _notifyIcon.ContextMenuStrip = contextMenu;

                // 处理托盘图标点击事件
                _notifyIcon.MouseClick += (sender, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        ShowMainWindow();
                    }
                };
                
                // 显示托盘通知
                ShowNotification("PigPicPot", "PigPicPot在你的托盘中！访问托盘以使用其主要功能。", ToolTipIcon.Info);
                
                LoggingHelper.Log("Tray icon initialized successfully.");
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error initializing tray icon");
                Console.WriteLine($"Error initializing tray icon: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示主窗口
        /// Show main window
        /// </summary>
        private void ShowMainWindow()
        {
            LoggingHelper.Log("Showing main window.");
            _mainWindow.Show();
            _mainWindow.Activate();
            _mainWindow.WindowState = System.Windows.WindowState.Normal;
            
            // 确保内容可见
            var mainContentPanel = _mainWindow.FindName("MainContentPanel") as DockPanel;
            var loadingOverlay = _mainWindow.FindName("LoadingOverlay") as Border;
            if (mainContentPanel != null && loadingOverlay != null)
            {
                // 如果加载覆盖层可见，隐藏它并显示主内容
                if (loadingOverlay.Visibility == Visibility.Visible)
                {
                    loadingOverlay.Visibility = Visibility.Collapsed;
                    mainContentPanel.Visibility = Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// 退出应用程序
        /// Exit application
        /// </summary>
        private void ExitApplication()
        {
            LoggingHelper.Log("Exiting application.");
            _notifyIcon?.Dispose();
            Application.Current.Shutdown();
        }

        /// <summary>
        /// 显示托盘通知
        /// Show tray notification
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知内容</param>
        /// <param name="icon">图标类型</param>
        public void ShowNotification(string title, string message, ToolTipIcon icon)
        {
            // 限制托盘通知最多弹出两次
            if (_trayNotificationCount < MaxTrayNotifications)
            {
                _notifyIcon?.ShowBalloonTip(3000, title, message, icon);
                _trayNotificationCount++;
            }
        }

        /// <summary>
        /// 释放资源
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _notifyIcon?.Dispose();
        }

        /// <summary>
        /// 检查是否需要启动时最小化到托盘
        /// Check if need to start minimized in tray
        /// </summary>
        public void CheckStartInTray()
        {
            LoggingHelper.Log("Checking start in tray setting...");
            // 这个方法现在只用于在UI加载完成后检查配置
            // The logic has been moved to App.xaml.cs
            LoggingHelper.Log("Start in tray check completed in App.xaml.cs");
        }
    }
}