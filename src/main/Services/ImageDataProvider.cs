using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using PigPicPot.Models;
using System;
using System.Reflection;
using System.Diagnostics;

namespace PigPicPot.Services
{
    /// <summary>
    /// 图像数据提供者，负责加载和管理图像数据
    /// Image data provider, responsible for loading and managing image data
    /// </summary>
    public class ImageDataProvider : IImageDataProvider
    {
        /// <summary>
        /// 所有图像项的只读集合
        /// Read-only collection of all image items
        /// </summary>
        public ReadOnlyCollection<ImageItem> AllImageItems { get; private set; } = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());

        /// <summary>
        /// 异步加载图像数据
        /// Asynchronously load image data
        /// </summary>
        /// <param name="directoriesToScan">需要扫描的目录列表</param>
        public async Task LoadAsync(IEnumerable<string> directoriesToScan)
        {
            await Task.Run(() =>
            {
                var allItems = new List<ImageItem>();
                foreach (var dir in directoriesToScan)
                {
                    if (Directory.Exists(dir))
                    {
                        // 获取目录中的所有图像文件（排除背景图片）
                        // Get all image files in directory (excluding background images)
                        var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => IsImageFile(f))
                            .Where(f => !IsBackgroundImage(f))
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

                string displayName = fileName;
                string baseEnglishName = "";
                string baseChineseName = "";
                string variantNumber = "";
                bool hasVariant = false;

                if (nameWithoutExt.StartsWith("pig_"))
                {
                    string name = nameWithoutExt.Substring(4);

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
                        string englishPart = name.Substring(0, firstChineseIndex).TrimEnd('_');
                        string chinesePart = name.Substring(firstChineseIndex);

                        var chineseMatch = System.Text.RegularExpressions.Regex.Match(chinesePart, @"^([^_\d]+)(\d+)?");
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

                        baseEnglishName = System.Text.RegularExpressions.Regex.Replace(englishPart, @"[\d_-]+$", "").TrimEnd('_');

                        displayName = baseChineseName;
                        if (hasVariant)
                        {
                            displayName += variantNumber;
                        }
                    }
                    else
                    {
                        baseEnglishName = System.Text.RegularExpressions.Regex.Replace(name, @"[\d_-]+$", "").TrimEnd('_');
                        displayName = baseEnglishName;
                    }
                }

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

                var imageItem = new ImageItem
                {
                    FilePath = filePath,
                    FileName = displayName,
                    IsAnimated = Path.GetExtension(filePath).Equals(".gif", StringComparison.OrdinalIgnoreCase),
                    Tags = tags,
                    SeriesTag = baseEnglishName,
                    BaseChineseName = baseChineseName,
                    VariantNumber = variantNumber,
                    HasVariant = hasVariant,
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
}