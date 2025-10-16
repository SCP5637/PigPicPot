using System;
using System.IO;
using System.Linq;
using PigPicPot.Helpers;
using PigPicPot.Views;

namespace PigPicPot
{
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// 应用程序启动时执行的初始化操作
        /// Application startup initialization
        /// </summary>
        /// <param name="e">启动事件参数</param>
        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化路径管理器
            // Initialize path manager
            PathManager.Initialize(e.Args);

            // 显示调试控制台
            // Show debug console
            DebugConsole.Show();

            try
            {
                string configFile = Path.Combine(PathManager.DataRoot, "usersettings.json");

                // 如果配置文件不存在，则创建默认配置
                // Create default configuration if config file doesn't exist
                if (!File.Exists(configFile))
                {
                    string defaultConfig =
@"# Set to true to show a debug console on startup
debug=false

# Set language to zh-CN for Chinese, or en for English
language=zh-CN

# Set background image path (relative to the exe's location)
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

# --- Update Settings ---
# Set to false to disable automatic update checks
check_for_updates=true
";
                    File.WriteAllText(configFile, defaultConfig);
                }

                // 读取配置文件并设置语言
                // Read config file and set language
                var config = File.ReadAllLines(configFile);
                var langLine = config.FirstOrDefault(line => line.StartsWith("language="));
                if (langLine != null)
                {
                    var langCode = langLine.Split('=')[1].Trim();
                    var culture = new System.Globalization.CultureInfo(langCode);
                    System.Threading.Thread.CurrentThread.CurrentUICulture = culture;
                    PigPicPot.Strings.Resources.Culture = culture;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during startup configuration: {ex.Message}");
            }
        }
    }
}