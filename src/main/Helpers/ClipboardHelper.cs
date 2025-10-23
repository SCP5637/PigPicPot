using System;
using System.IO;
using System.Text;
using System.Collections.Generic; // 添加这个using语句

using System.Reflection;
using System.Diagnostics;
using PigPicPot.Strings; // 添加这个using语句

namespace PigPicPot.Helpers
{
    /// <summary>
    /// 剪贴板助手类，提供与剪贴板操作相关的功能
    /// Clipboard helper class, provides clipboard-related functionality
    /// </summary>
    public static class ClipboardHelper
    {
        // 添加一个静态字典来跟踪已创建的临时文件
        private static readonly Dictionary<string, string> _tempFiles = new Dictionary<string, string>();
        
        /// <summary>
        /// 设置动画GIF到剪贴板
        /// Set animated GIF to clipboard
        /// </summary>
        /// <param name="gifFilePath">GIF文件路径</param>
        public static void SetAnimatedGif(string gifFilePath)
        {
            if (!File.Exists(gifFilePath))
            {
                // 修复资源引用问题
                throw new FileNotFoundException("GIF file not found", gifFilePath);
            }

            // 创建临时目录并复制GIF文件
            // Create temporary directory and copy GIF file
            string tempDir = Path.Combine(GetApplicationRoot(), "resource", "temp");
            Directory.CreateDirectory(tempDir);
            string tempGifPath = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".gif");
            
            // 修复：检查是否已经存在相同源文件的临时文件
            string? existingTempFile = FindExistingTempFile(gifFilePath);
            if (!string.IsNullOrEmpty(existingTempFile) && File.Exists(existingTempFile))
            {
                tempGifPath = existingTempFile;
            }
            else
            {
                File.Copy(gifFilePath, tempGifPath, true);
                // 记录新创建的临时文件
                _tempFiles[gifFilePath] = tempGifPath;
            }

            string htmlSource = $"<img src=\"file:///{tempGifPath.Replace('\\', '/')}\" />";

            string clipboardHtml = GetClipboardHtmlFormat(htmlSource);

            DataObject dataObject = new DataObject();
            dataObject.SetData(DataFormats.Html, clipboardHtml);
            dataObject.SetData(DataFormats.Text, $"GIF Image: {tempGifPath ?? string.Empty}");

            System.Windows.Clipboard.SetDataObject(dataObject, true);
        }

        // 新增：查找已存在的临时文件
        private static string? FindExistingTempFile(string originalFilePath)
        {
            if (_tempFiles.TryGetValue(originalFilePath, out string? tempPath))
            {
                return tempPath;
            }
            return null;
        }
        
        /// <summary>
        /// 清理临时文件
        /// </summary>
        public static void CleanupTempFiles()
        {
            try
            {
                foreach (var tempFile in _tempFiles.Values)
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                _tempFiles.Clear();
                
                string tempDir = Path.Combine(GetApplicationRoot(), "resource", "temp");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up temp files: {ex.Message}");
            }
        }
        
        private static string GetApplicationRoot()
        {
            string? exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Assembly.GetEntryAssembly()?.Location 
                ?? AppDomain.CurrentDomain.BaseDirectory;
            
            string? exeDir = Path.GetDirectoryName(exePath);
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
            result += finalHtml.Substring(finalHtml.IndexOf("<html>", StringComparison.Ordinal));

            return result;
        }
    }
}