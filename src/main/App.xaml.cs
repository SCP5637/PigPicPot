using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using PigPicPot.Helpers;
using PigPicPot.Views;

namespace PigPicPot
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private SplashScreenWindow? _splashScreen;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 初始化路径管理器
            PathManager.Initialize(e.Args);
            
            // 确保配置文件存在
            string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
            EnsureConfigurationFileExists(configFile);
            
            // 设置应用程序语言
            SetApplicationLanguage(configFile);
            
            // 显示启动画面
            ShowSplashScreen();
            
            // 初始化主窗口
            LoggingHelper.Log("Initializing MainWindow...");
            _mainWindow = new MainWindow();
            _mainWindow.InitializationCompleted += OnMainWindowInitializationCompleted;
            _mainWindow.StartInitialization();
        }

        private void App_Exit(object sender, ExitEventArgs e)
        {
            LoggingHelper.Log("Application is shutting down.");
            HotkeyHelper.UnregisterGlobalHotkey();
            HideSplashScreen();
            Shutdown();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            LoggingHelper.LogException(e.Exception, "An unhandled exception occurred on the UI thread.");
            e.Handled = true; // 标记为已处理，以防止默认的系统崩溃对话框
            HideSplashScreen();
            Shutdown();
        }

        private void EnsureConfigurationFileExists(string configFile)
        {
            LoggingHelper.Log($"Checking configuration file: {configFile}");

            if (!File.Exists(configFile))
            {
                LoggingHelper.Log("Configuration file not found, creating default configuration...");
                string defaultConfig = @"{
  ""debug"": false,
  ""language"": ""zh-CN"",
  ""background_image"": ""resource/zhu3.jpg"",
  ""lock_resolution"": false,
  ""width"": 1366,
  ""height"": 768,
  ""mini_mode_background"": ""resource/zhu1.png"",
  ""mini_mode_width"": 640,
  ""mini_mode_height"": 480,
  ""mini_mode_hotkey"": ""LeftCtrl+LeftAlt+B"",
  ""reset_mini_mode_state"": true,
  ""check_for_updates"": true,
  ""start_in_tray"": true,
  ""max_cache_size_mb"": -1,
  ""max_cache_items"": -1,
  ""inactivity_reset_time"": 150
}";
                File.WriteAllText(configFile, defaultConfig);
                LoggingHelper.Log("Default configuration file created.");
            }
        }

        private void SetApplicationLanguage(string configFile)
        {
            try
            {
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                if (jsonDoc.RootElement.TryGetProperty("language", out var langElement))
                {
                    var langCode = langElement.GetString();
                    if (!string.IsNullOrEmpty(langCode))
                    {
                        var culture = new System.Globalization.CultureInfo(langCode);
                        System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                        PigPicPot.Strings.Resources.Culture = culture;
                        LoggingHelper.Log($"Language set to: {langCode}");
                    }
                }
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                LoggingHelper.LogException(jsonEx, "Failed to parse JSON config for language, trying INI format");
                var config = File.ReadAllLines(configFile);
                var langLine = config.FirstOrDefault(line => line.StartsWith("language="));
                if (langLine != null)
                {
                    var langCode = langLine.Split('=')[1].Trim();
                    var culture = new System.Globalization.CultureInfo(langCode);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                    PigPicPot.Strings.Resources.Culture = culture;
                    LoggingHelper.Log($"Language set to: {langCode}");
                }
            }
        }

        private void OnMainWindowInitializationCompleted(object? sender, EventArgs e)
        {
            LoggingHelper.Log("MainWindow initialization completed.");
            
            if (_mainWindow == null) return;

            // 取消订阅事件
            _mainWindow.InitializationCompleted -= OnMainWindowInitializationCompleted;

            string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
            
            // 注册全局热键
            RegisterGlobalHotkey(configFile);

            // 根据窗口自身的状态决定是否显示
            if (!_mainWindow.StartHidden)
            {
                _mainWindow.Show();
                LoggingHelper.Log("MainWindow shown.");
            }
            else
            {
                LoggingHelper.Log("Starting in tray mode, window will not be shown (based on StartHidden property).");
            }

            // 隐藏启动画面
            HideSplashScreen();
        }

        private void ShowSplashScreen()
        {
            _splashScreen = new SplashScreenWindow();
            _splashScreen.Show();
        }

        public void UpdateSplashScreen(string status, int percentage)
        {
            if (_splashScreen != null)
            {
                _splashScreen.Dispatcher.Invoke(() => _splashScreen.UpdateProgress(status, percentage));
            }
        }

        public void HideSplashScreen()
        {
            if (_splashScreen != null)
            {
                _splashScreen.Dispatcher.Invoke(() => _splashScreen.Hide());
                _splashScreen = null;
            }
        }

        private void RegisterGlobalHotkey(string configFile)
        {
            try
            {
                // 注销之前的全局热键（如果有的话）
                HotkeyHelper.UnregisterGlobalHotkey();

                // 读取热键配置
                string hotkeyStr = "LeftCtrl+LeftAlt+B"; // 默认热键
                if (File.Exists(configFile))
                {
                    try
                    {
                        // 尝试读取JSON格式的配置文件
                        string jsonContent = File.ReadAllText(configFile);
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                        if (jsonDoc.RootElement.TryGetProperty("mini_mode_hotkey", out var hotkeyElement))
                        {
                            hotkeyStr = hotkeyElement.GetString() ?? hotkeyStr;
                        }
                    }
                    catch (System.Text.Json.JsonException jsonEx)
                    {
                        // 如果JSON解析失败，尝试使用旧的INI格式解析
                        LoggingHelper.LogException(jsonEx, "Failed to parse JSON config for hotkey, trying INI format");
                        var config = File.ReadAllLines(configFile)
                            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                            .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                        hotkeyStr = config.TryGetValue("mini_mode_hotkey", out var hotkeyConfig) ? hotkeyConfig : hotkeyStr;
                    }
                }

                // 注册热键
                var parts = hotkeyStr.Split('+');
                if (parts.Length >= 2)
                {
                    var key = (Key)Enum.Parse(typeof(Key), parts.Last(), true);
                    ModifierKeys modifiers = ModifierKeys.None;
                    foreach (var modStr in parts.Take(parts.Length - 1))
                    {
                        if (modStr.Contains("Ctrl", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Control;
                        if (modStr.Contains("Alt", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Alt;
                        if (modStr.Contains("Shift", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Shift;
                        if (modStr.Contains("Win", StringComparison.OrdinalIgnoreCase)) modifiers |= ModifierKeys.Windows;
                    }

                    bool success = HotkeyHelper.RegisterGlobalHotkey(modifiers, key, ToggleMiniMode);
                    if (success)
                    {
                        LoggingHelper.Log($"Global hotkey registered: {hotkeyStr}");
                    }
                    else
                    {
                        LoggingHelper.Log($"Failed to register global hotkey: {hotkeyStr}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Failed to register global hotkey, using default");
                try
                {
                    // 使用默认热键
                    bool success = HotkeyHelper.RegisterGlobalHotkey(ModifierKeys.Control | ModifierKeys.Alt, Key.B, ToggleMiniMode);
                    if (success)
                    {
                        LoggingHelper.Log("Default global hotkey registered successfully");
                    }
                    else
                    {
                        LoggingHelper.Log("Failed to register default global hotkey");
                    }
                }
                catch (Exception defaultEx)
                {
                    LoggingHelper.LogException(defaultEx, "Error registering default global hotkey");
                    LoggingHelper.Log("Continuing without global hotkey functionality");
                }
            }
        }

        private void ToggleMiniMode()
        {
            LoggingHelper.Log("Toggle mini mode from global hotkey");
            if (_mainWindow != null)
            {
                _mainWindow.Dispatcher.Invoke(() =>
                {
                    _mainWindow.ToggleMiniMode();
                });
            }
        }

        private bool ShouldStartInTray(string configFile)
        {
            try
            {
                if (!File.Exists(configFile))
                    return true; // 默认改为true

                try
                {
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
                catch (System.Text.Json.JsonException jsonEx)
                {
                    // 如果JSON解析失败，尝试使用旧的INI格式解析
                    LoggingHelper.LogException(jsonEx, "Failed to parse JSON config for start_in_tray, trying INI format");
                    var config = File.ReadAllLines(configFile)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                        .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                    return config.TryGetValue("start_in_tray", out var startInTrayStr) &&
                           startInTrayStr.ToLower() == "true";
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error checking start in tray setting");
                return true; // 默认改为true
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingHelper.Log("Application shutting down...");
            // 确保主窗口被正确关闭
            _mainWindow?.Close();
            // 注销全局热键
            HotkeyHelper.UnregisterGlobalHotkey();
            // 清理临时文件
            Helpers.ClipboardHelper.CleanupTempFiles();
            base.OnExit(e);
        }

        /// <summary>
        /// 获取主窗口实例
        /// </summary>
        public new MainWindow? MainWindow => _mainWindow;
    }
}