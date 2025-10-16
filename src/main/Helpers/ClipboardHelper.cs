using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Diagnostics;

namespace PigPicPot.Helpers
{
    /// <summary>
    /// 剪贴板助手类，提供与剪贴板操作相关的功能
    /// Clipboard helper class, provides clipboard-related functionality
    /// </summary>
    public static class ClipboardHelper
    {
        /// <summary>
        /// 设置动画GIF到剪贴板
        /// Set animated GIF to clipboard
        /// </summary>
        /// <param name="gifFilePath">GIF文件路径</param>
        public static void SetAnimatedGif(string gifFilePath)
        {
            if (!File.Exists(gifFilePath))
            {
                throw new FileNotFoundException("指定的GIF文件不存在。", gifFilePath);
            }

            // 创建临时目录并复制GIF文件
            // Create temporary directory and copy GIF file
            string tempDir = Path.Combine(GetApplicationRoot(), "resource", "temp");
            Directory.CreateDirectory(tempDir);
            string tempGifPath = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".gif");
            File.Copy(gifFilePath, tempGifPath, true);

            string htmlSource = $"<img src=\"file:///{tempGifPath.Replace('\\', '/')}\" />";

            string clipboardHtml = GetClipboardHtmlFormat(htmlSource);

            DataObject dataObject = new DataObject();
            dataObject.SetData(DataFormats.Html, clipboardHtml);
            dataObject.SetData(DataFormats.Text, $"GIF Image: {tempGifPath ?? string.Empty}");

            Clipboard.SetDataObject(dataObject, true);
        }

        private static string GetApplicationRoot()
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Assembly.GetEntryAssembly()?.Location 
                ?? AppDomain.CurrentDomain.BaseDirectory;
            
            string exeDir = Path.GetDirectoryName(exePath);
            return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string GetClipboardHtmlFormat(string htmlFragment)
        {
            var sb = new StringBuilder();

            string header = @"Version:0.9
StartHTML:{0:000000}
EndHTML:{1:000000}
StartFragment:{2:000000}
EndFragment:{3:000000}
";

            string htmlPrefix = "<html><body><!--StartFragment-->";
            string htmlSuffix = "<!--EndFragment--></body></html>";

            sb.Append(string.Format(header, 0, 0, 0, 0));
            sb.Append(htmlPrefix);
            int fragmentStart = sb.Length;
            sb.Append(htmlFragment);
            int fragmentEnd = sb.Length;
            sb.Append(htmlSuffix);

            string finalHtml = sb.ToString();
            int startHtml = finalHtml.IndexOf("<html>");
            int endHtml = finalHtml.Length;

            string result = string.Format(header, startHtml, endHtml, fragmentStart, fragmentEnd);
            result += finalHtml.Substring(finalHtml.IndexOf("<html>"));

            return result;
        }
    }
}