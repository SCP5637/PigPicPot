using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PigPicPot.Core;
using PigPicPot.Helpers;
using PigPicPot.Messaging;

namespace PigPicPot.Services
{
    /// <summary>
    /// 配置服务，负责处理应用程序配置
    /// Configuration service, responsible for handling application configuration
    /// </summary>
    public class ConfigurationService
    {
        /// <summary>
        /// 加载配置
        /// Load configuration
        /// </summary>
        /// <param name="window">主窗口</param>
        public void LoadConfiguration(Window window)
        {
            LoggingHelper.Log("Loading configuration...");
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile))
                {
                    LoggingHelper.Log("Configuration file not found, skipping configuration load.");
                    return;
                }

                // 尝试读取JSON格式的配置文件
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                var config = new Dictionary<string, string>();
                
                // 解析JSON配置
                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String || 
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.Number ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.True ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        config[property.Name] = property.Value.ToString();
                    }
                }

                if (config.TryGetValue("lock_resolution", out var lockResStr) && lockResStr == "true")
                {
                    window.Width = config.TryGetValue("width", out var w) && int.TryParse(w, out int width) ? width : 1366;
                    window.Height = config.TryGetValue("height", out var h) && int.TryParse(h, out int height) ? height : 768;
                    window.ResizeMode = ResizeMode.NoResize;
                    LoggingHelper.Log($"Window size locked to {window.Width}x{window.Height}");
                }
                else
                {
                    window.Width = 1366;
                    window.Height = 768;
                    window.ResizeMode = ResizeMode.CanResize;
                    LoggingHelper.Log("Window size unlocked, using default size 1366x768");
                }

                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathManager.AppRoot, bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        var backgroundBrush = window.FindName("BackgroundImageBrush") as ImageBrush;
                        if (backgroundBrush != null) backgroundBrush.ImageSource = bgBitmap;
                        SpecialFeatures.CheckAndEnableFeatures(window, fullBgPath, bgBitmap.PixelWidth, bgBitmap.PixelHeight);
                        AdjustTextColors(window, fullBgPath);
                        LoggingHelper.Log($"Background image loaded: {fullBgPath}");
                    }
                    else
                    {
                        LoggingHelper.Log($"Background image not found: {fullBgPath}");
                    }
                }
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                // 如果JSON解析失败，尝试使用旧的INI格式解析
                LoggingHelper.LogException(jsonEx, "Failed to parse JSON config, trying INI format");
                LoadConfigurationIniFormat(window);
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error loading configuration");
            }
        }

        /// <summary>
        /// 以INI格式加载配置（向后兼容）
        /// Load configuration in INI format (backward compatibility)
        /// </summary>
        /// <param name="window">主窗口</param>
        private void LoadConfigurationIniFormat(Window window)
        {
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile))
                {
                    return;
                }

                var config = File.ReadAllLines(configFile)
                                 .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                                 .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                if (config.TryGetValue("lock_resolution", out var lockResStr) && lockResStr == "true")
                {
                    window.Width = config.TryGetValue("width", out var w) && int.TryParse(w, out int width) ? width : 1366;
                    window.Height = config.TryGetValue("height", out var h) && int.TryParse(h, out int height) ? height : 768;
                    window.ResizeMode = ResizeMode.NoResize;
                    LoggingHelper.Log($"Window size locked to {window.Width}x{window.Height}");
                }
                else
                {
                    window.Width = 1366;
                    window.Height = 768;
                    window.ResizeMode = ResizeMode.CanResize;
                    LoggingHelper.Log("Window size unlocked, using default size 1366x768");
                }

                if (config.TryGetValue("background_image", out var bgPathValue))
                {
                    string fullBgPath = Path.Combine(PathManager.AppRoot, bgPathValue.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(fullBgPath))
                    {
                        var bgBitmap = new BitmapImage();
                        bgBitmap.BeginInit();
                        bgBitmap.UriSource = new Uri(fullBgPath);
                        bgBitmap.EndInit();
                        var backgroundBrush = window.FindName("BackgroundImageBrush") as ImageBrush;
                        if (backgroundBrush != null) backgroundBrush.ImageSource = bgBitmap;
                        SpecialFeatures.CheckAndEnableFeatures(window, fullBgPath, bgBitmap.PixelWidth, bgBitmap.PixelHeight);
                        AdjustTextColors(window, fullBgPath);
                        LoggingHelper.Log($"Background image loaded: {fullBgPath}");
                    }
                    else
                    {
                        LoggingHelper.Log($"Background image not found: {fullBgPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error loading INI format configuration");
            }
        }

        /// <summary>
        /// 根据背景图片调整文本颜色
        /// Adjust text colors based on background image
        /// </summary>
        /// <param name="window">主窗口</param>
        /// <param name="backgroundImagePath">背景图片路径</param>
        private void AdjustTextColors(Window window, string backgroundImagePath)
        {
            try
            {
                // 计算背景图片的平均亮度
                double brightness = CalculateAverageBrightness(backgroundImagePath);
                
                // 根据亮度调整文本颜色
                var foregroundColor = brightness > 0.5 ? Colors.Black : Colors.White;
                
                // 获取需要调整颜色的文本控件
                var loadSummaryTextBlock = window.FindName("LoadSummaryTextBlock") as System.Windows.Controls.TextBlock;
                var summaryTextBlock = window.FindName("SummaryTextBlock") as System.Windows.Controls.TextBlock;
                
                if (loadSummaryTextBlock != null)
                {
                    loadSummaryTextBlock.Foreground = new SolidColorBrush(foregroundColor);
                }
                
                if (summaryTextBlock != null)
                {
                    summaryTextBlock.Foreground = new SolidColorBrush(foregroundColor);
                }
                
                LoggingHelper.Log($"Text colors adjusted based on background brightness: {brightness:F2}");
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error adjusting text colors based on background image");
            }
        }

        /// <summary>
        /// 计算图片的平均亮度
        /// Calculate average brightness of an image
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        /// <returns>平均亮度值 (0-1)</returns>
        private double CalculateAverageBrightness(string imagePath)
        {
            try
            {
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = stream;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // 为了提高性能，我们缩小图片进行采样
                    var reducedBitmap = new TransformedBitmap(bitmap, new ScaleTransform(0.1, 0.1));
                    
                    var width = reducedBitmap.PixelWidth;
                    var height = reducedBitmap.PixelHeight;
                    var pixels = new byte[width * height * 4]; // 4 channels: BGRA
                    reducedBitmap.CopyPixels(pixels, width * 4, 0);

                    // 计算平均亮度
                    double totalBrightness = 0;
                    int pixelCount = 0;
                    
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        // 获取BGR值
                        byte blue = pixels[i];
                        byte green = pixels[i + 1];
                        byte red = pixels[i + 2];
                        
                        // 使用感知亮度公式计算亮度
                        double brightness = (0.299 * red + 0.587 * green + 0.114 * blue) / 255.0;
                        totalBrightness += brightness;
                        pixelCount++;
                    }
                    
                    return totalBrightness / pixelCount;
                }
            }
            catch
            {
                // 如果无法计算亮度，默认返回0.5（中等亮度）
                return 0.5;
            }
        }

        /// <summary>
        /// 获取热键配置
        /// Get hotkey configuration
        /// </summary>
        /// <returns>热键字符串</returns>
        public string GetHotkeyFromConfig()
        {
            LoggingHelper.Log("Getting hotkey from configuration...");
            string defaultHotkey = "LeftCtrl+LeftAlt+B";
            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                if (!File.Exists(configFile))
                {
                    LoggingHelper.Log("Configuration file not found, using default hotkey.");
                    return defaultHotkey;
                }

                // 尝试读取JSON格式的配置文件
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                var config = new Dictionary<string, string>();
                
                // 解析JSON配置
                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String || 
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.Number ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.True ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        config[property.Name] = property.Value.ToString();
                    }
                }

                string hotkey = config.TryGetValue("mini_mode_hotkey", out var hotkeyStr) ? hotkeyStr : defaultHotkey;
                LoggingHelper.Log($"Hotkey from configuration: {hotkey}");
                return hotkey;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                // 如果JSON解析失败，尝试使用旧的INI格式解析
                LoggingHelper.LogException(jsonEx, "Failed to parse JSON config for hotkey, trying INI format");
                try
                {
                    string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
                    var config = File.ReadAllLines(configFile)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                        .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());

                    string hotkey = config.TryGetValue("mini_mode_hotkey", out var hotkeyStr) ? hotkeyStr : defaultHotkey;
                    LoggingHelper.Log($"Hotkey from INI configuration: {hotkey}");
                    return hotkey;
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogException(ex, "Error getting hotkey from INI configuration, using default");
                    return defaultHotkey;
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error getting hotkey from configuration, using default");
                return defaultHotkey;
            }
        }

        /// <summary>
        /// 获取配置字典
        /// Get configuration dictionary
        /// </summary>
        /// <returns>配置字典</returns>
        public Dictionary<string, string> GetConfig()
        {
            LoggingHelper.Log("Getting configuration...");
            string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");
            if (!File.Exists(configFile))
            {
                LoggingHelper.Log("Configuration file not found.");
                return new Dictionary<string, string>();
            }

            try
            {
                // 尝试读取JSON格式的配置文件
                string jsonContent = File.ReadAllText(configFile);
                var jsonDoc = System.Text.Json.JsonDocument.Parse(jsonContent);
                var config = new Dictionary<string, string>();
                
                // 解析JSON配置
                foreach (var property in jsonDoc.RootElement.EnumerateObject())
                {
                    if (property.Value.ValueKind == System.Text.Json.JsonValueKind.String || 
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.Number ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.True ||
                        property.Value.ValueKind == System.Text.Json.JsonValueKind.False)
                    {
                        config[property.Name] = property.Value.ToString();
                    }
                }
                
                LoggingHelper.Log($"JSON configuration loaded with {config.Count} entries.");
                return config;
            }
            catch (System.Text.Json.JsonException jsonEx)
            {
                // 如果JSON解析失败，尝试使用旧的INI格式解析
                LoggingHelper.LogException(jsonEx, "Failed to parse JSON config, trying INI format");
                try
                {
                    var config = File.ReadAllLines(configFile)
                        .Where(line => !string.IsNullOrWhiteSpace(line) && !line.Trim().StartsWith("#") && line.Contains('='))
                        .ToDictionary(line => line.Split('=')[0].Trim(), line => line.Split('=')[1].Trim());
                    LoggingHelper.Log($"INI configuration loaded with {config.Count} entries.");
                    return config;
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogException(ex, "Error loading INI configuration");
                    return new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error getting configuration");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 注册热键
        /// Register hotkey
        /// </summary>
        /// <param name="hotkeyHelper">热键助手</param>
        /// <param name="hotkeyStr">热键字符串</param>
        /// <param name="action">热键触发的操作</param>
        /// <param name="messenger">消息传递器</param>
        public void RegisterHotkey(HotkeyHelper hotkeyHelper, string hotkeyStr, Action action, IMessenger? messenger)
        {
            try
            {
                LoggingHelper.Log($"Registering hotkey: {hotkeyStr}");
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
                
                bool success = hotkeyHelper.Register(modifiers, key, () => action());
                if (success)
                {
                    LoggingHelper.Log("Hotkey registered successfully.");
                }
                else
                {
                    LoggingHelper.Log("Failed to register hotkey.");
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Error registering hotkey");
                try
                {
                    // 尝试使用默认热键
                    bool success = hotkeyHelper.Register(ModifierKeys.Control | ModifierKeys.Alt, Key.B, () => action());
                    if (success)
                    {
                        // 通过消息系统通知用户
                        messenger?.Send(new ShowNotificationMessage(PigPicPot.Strings.Resources.InvalidHotkeyConfig));
                        LoggingHelper.Log("Default hotkey registered successfully.");
                    }
                    else
                    {
                        LoggingHelper.Log("Failed to register default hotkey.");
                    }
                }
                catch (Exception defaultEx)
                {
                    LoggingHelper.LogException(defaultEx, "Error registering default hotkey");
                    // 如果默认热键也失败，只记录日志，不中断程序
                    LoggingHelper.Log("Hotkey registration failed, continuing without hotkey functionality.");
                }
            }
        }
    }
}