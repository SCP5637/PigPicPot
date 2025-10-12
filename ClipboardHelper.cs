using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

public static class ClipboardHelper
{
    /// <summary>
    /// 将GIF文件以保留动画的形式放入剪贴板。
    /// </summary>
    /// <param name="gifFilePath">本地GIF文件的绝对路径。</param>
    public static void SetAnimatedGif(string gifFilePath)
    {
        if (!File.Exists(gifFilePath))
        {
            throw new FileNotFoundException("指定的GIF文件不存在。", gifFilePath);
        }

        // 1. 创建一个临时的、唯一的GIF文件路径，防止原始文件被锁定或移动
        string tempDir = Path.Combine(GetApplicationRoot(), "resource", "temp");
        Directory.CreateDirectory(tempDir);
        string tempGifPath = Path.Combine(tempDir, Guid.NewGuid().ToString() + ".gif");
        File.Copy(gifFilePath, tempGifPath, true);

        // 2. 构建指向临时文件的HTML代码
        string htmlSource = $"<img src=\"file:///{tempGifPath.Replace('\\', '/')}\" />";

        // 3. 将HTML代码包装成剪贴板所需的CF_HTML格式
        string clipboardHtml = GetClipboardHtmlFormat(htmlSource);
        
        // 4. 创建一个DataObject，同时提供HTML和纯文本两种格式
        //    这样可以确保在不支持HTML粘贴的编辑器中也能粘贴出文本内容
        DataObject dataObject = new DataObject();
        dataObject.SetData(DataFormats.Html, clipboardHtml);
        dataObject.SetData(DataFormats.Text, $"GIF Image: {tempGifPath}");

        // 5. 将DataObject放入剪贴板
        //    设置第二个参数为true，以便在应用程序退出后数据仍保留在剪贴板上
        Clipboard.SetDataObject(dataObject, true);
    }

    private static string GetApplicationRoot()
    {
        string? exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        string? exeDir = Path.GetDirectoryName(exePath);
        return exeDir ?? AppDomain.CurrentDomain.BaseDirectory;
    }

    /// <summary>
    /// 生成符合剪贴板标准的CF_HTML格式字符串。
    /// </summary>
    /// <param name="htmlFragment">要放入剪贴板的HTML代码片段。</param>
    /// <returns>带有剪贴板所需头部的完整HTML字符串。</returns>
    private static string GetClipboardHtmlFormat(string htmlFragment)
    {
        var sb = new StringBuilder();
        
        // CF_HTML头部信息
        string header = @"Version:0.9
StartHTML:{0:000000}
EndHTML:{1:000000}
StartFragment:{2:000000}
EndFragment:{3:000000}
";
        
        string htmlPrefix = "<html><body><!--StartFragment-->";
        string htmlSuffix = "<!--EndFragment--></body></html>";
        
        // 拼接完整的HTML文档
        sb.Append(string.Format(header, 0, 0, 0, 0)); // 占位符
        sb.Append(htmlPrefix);
        int fragmentStart = sb.Length;
        sb.Append(htmlFragment);
        int fragmentEnd = sb.Length;
        sb.Append(htmlSuffix);

        // 计算并替换头部的字节位置
        // 注意：所有位置都是从字符串开头计算的字节数
        string finalHtml = sb.ToString();
        int startHtml = finalHtml.IndexOf("<html>");
        int endHtml = finalHtml.Length;

        // 用实际的字节偏移量替换占位符
        string result = string.Format(header, startHtml, endHtml, fragmentStart, fragmentEnd);
        result += finalHtml.Substring(finalHtml.IndexOf("<html>"));

        return result;
    }
}
