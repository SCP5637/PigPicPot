using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public class ImageDataProvider : IImageDataProvider
{
    public ReadOnlyCollection<ImageItem> AllImageItems { get; private set; } = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());

    public async Task LoadAsync(IEnumerable<string> directoriesToScan)
    {
        await Task.Run(() =>
        {
            var allItems = new List<ImageItem>();
            foreach (var dir in directoriesToScan)
            {
                if (Directory.Exists(dir))
                {
                    var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => IsImageFile(f))
                        .Where(f => !IsBackgroundImage(f)) // Keep this filter
                        .ToList();

                    foreach (var file in files)
                    {
                        var imageItem = CreateImageItem(file);
                        if (imageItem != null)
                        {
                            allItems.Add(imageItem);
                        }
                    }
                }
            }
            AllImageItems = new ReadOnlyCollection<ImageItem>(allItems);
        });
    }

    private ImageItem? CreateImageItem(string filePath)
    {
        try
        {
            var fileName = Path.GetFileName(filePath);
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            
            // 解析文件名
            string displayName = fileName; // 默认使用文件名
            string baseEnglishName = "";
            string baseChineseName = "";
            string variantNumber = "";
            bool hasVariant = false;

            if (nameWithoutExt.StartsWith("pig_"))
            {
                string name = nameWithoutExt.Substring(4); // Remove "pig_"
                
                // 解析文件名格式: 英文名_中文名_数字 或 英文名_中文名数字
                // 首先找到中文部分的起始位置
                int firstChineseIndex = -1;
                for (int i = 0; i < name.Length; i++)
                {
                    if (name[i] >= 0x4E00 && name[i] <= 0x9FFF)
                    {
                        firstChineseIndex = i;
                        break;
                    }
                }

                if (firstChineseIndex != -1)
                {
                    // 分离英文和中文部分
                    string englishPart = name.Substring(0, firstChineseIndex).TrimEnd('_');
                    string chinesePart = name.Substring(firstChineseIndex);
                    
                    // 从中文部分提取数字变体
                    var chineseMatch = System.Text.RegularExpressions.Regex.Match(chinesePart, @"^([^\d]+)(\d+)?$");
                    if (chineseMatch.Success)
                    {
                        baseChineseName = chineseMatch.Groups[1].Value;
                        if (chineseMatch.Groups[2].Success)
                        {
                            variantNumber = chineseMatch.Groups[2].Value;
                            hasVariant = true;
                        }
                    }
                    else
                    {
                        baseChineseName = chinesePart;
                    }
                    
                    // 清理英文部分
                    baseEnglishName = System.Text.RegularExpressions.Regex.Replace(englishPart, @"[\d_-]+$", "").TrimEnd('_');
                    
                    // 显示名称使用中文部分
                    displayName = baseChineseName;
                    if (hasVariant)
                    {
                        displayName += variantNumber;
                    }
                }
                else
                {
                    // 只有英文部分
                    baseEnglishName = System.Text.RegularExpressions.Regex.Replace(name, @"[\d_-]+$", "").TrimEnd('_');
                    displayName = baseEnglishName;
                }
            }

            // 构建标签列表
            var tags = new List<string>();
            var directory = Path.GetDirectoryName(filePath);
            if (directory != null && directory.Contains("resource"))
            {
                var relativePath = directory.Substring(directory.IndexOf("resource") + "resource".Length);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Where(part => !string.IsNullOrEmpty(part))
                    .ToList();
                
                tags.AddRange(pathParts);
            }

            // 添加动态标签信息
            var imageItem = new ImageItem
            {
                FilePath = filePath,
                FileName = displayName,
                IsAnimated = Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase),
                Tags = tags,
                SeriesTag = baseEnglishName, // 使用英文名作为系列标签
                BaseChineseName = baseChineseName, // 基础中文名
                VariantNumber = variantNumber, // 变体编号
                HasVariant = hasVariant, // 是否有变体
                ThumbnailSource = null,
                IsThumbnailQueued = false
            };

            return imageItem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating ImageItem for {filePath}: {ex.Message}");
            return null;
        }
    }

    private bool IsImageFile(string filePath)
    {
        var extensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        return extensions.Contains(Path.GetExtension(filePath).ToLowerInvariant());
    }

    private bool IsBackgroundImage(string filePath)
    {
        var backgroundNames = new[] { "zhu3.jpg", "zhu1.png" };
        var fileName = Path.GetFileName(filePath);
        return backgroundNames.Contains(fileName, StringComparer.OrdinalIgnoreCase);
    }
}