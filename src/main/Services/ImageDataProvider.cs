using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using PigPicPot.Models;
using System;
using System.Reflection;
using System.Diagnostics;
using PigPicPot.Helpers;
using System.Security.Cryptography;

namespace PigPicPot.Services
{
    /// <summary>
    /// 图像数据提供者，负责加载和管理图像数据
    /// Image data provider, responsible for loading and managing image data
    /// </summary>
    public class ImageDataProvider : IImageDataProvider
    {
        private readonly ImageDatabaseService _databaseService;
        
        /// <summary>
        /// 所有图像项的只读集合
        /// Read-only collection of all image items
        /// </summary>
        public ReadOnlyCollection<ImageItem> AllImageItems { get; private set; } = new ReadOnlyCollection<ImageItem>(new List<ImageItem>());

        /// <summary>
        /// 构造函数，初始化图像数据库服务
        /// Constructor, initialize image database service
        /// </summary>
        public ImageDataProvider()
        {
            _databaseService = new ImageDatabaseService();
        }

        /// <summary>
        /// 异步加载图像数据
        /// Asynchronously load image data
        /// </summary>
        /// <param name="directoriesToScan">需要扫描的目录列表</param>
        public async Task LoadAsync(IEnumerable<string> directoriesToScan)
        {
            LoggingHelper.Log("ImageDataProvider.LoadAsync started.");
            var app = System.Windows.Application.Current as App;
            
            var allItems = new System.Collections.Concurrent.ConcurrentBag<ImageItem>();
            Dictionary<string, ImageItem>? dbImageDict = null;
            try
            {
                // 首先尝试从数据库加载所有图像
                app?.UpdateSplashScreen("正在从数据库加载图像信息...", 35);
                LoggingHelper.Log("ImageDataProvider.LoadAsync loading images from database.");
                var dbImages = await _databaseService.LoadAllImagesAsync();
                dbImageDict = dbImages.ToDictionary(img => img.FilePath ?? "", img => img);
                LoggingHelper.Log($"ImageDataProvider.LoadAsync {dbImages.Count} images loaded from database.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading images from database: {ex.Message}");
                app?.UpdateSplashScreen("数据库加载失败，使用全新加载...", 35);
            }
            
            var directories = directoriesToScan.ToList();
            int totalDirectories = directories.Count;
            int processedDirectories = 0;
            
            app?.UpdateSplashScreen("正在扫描图像文件...", 40);
            
            var itemsToSave = new System.Collections.Concurrent.ConcurrentBag<ImageItem>();

            LoggingHelper.Log("ImageDataProvider.LoadAsync starting parallel scan.");
            await Task.Run(() =>
            {
                Parallel.ForEach(directories, dir =>
                {
                    if (Directory.Exists(dir))
                    {
                        var files = Directory.GetFiles(dir, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(f => IsImageFile(f))
                            .Where(f => !IsBackgroundImage(f))
                            .ToList();

                        foreach (var file in files)
                        {
                            ImageItem? imageItem = null;
                            
                            try
                            {
                                if (dbImageDict?.TryGetValue(file, out var dbImage) == true)
                                {
                                    // 检查文件Hash是否匹配
                                    string currentHash = _databaseService.CalculateFileHash(file);
                                    if (dbImage.FileHash == currentHash)
                                    {
                                        // Hash匹配，直接使用数据库中的记录
                                        imageItem = dbImage;
                                    }
                                    else
                                    {
                                        // Hash不匹配，需要重新生成缩略图
                                        imageItem = CreateImageItem(file);
                                        if (imageItem != null)
                                        {
                                            imageItem.FileHash = currentHash;
                                            itemsToSave.Add(imageItem);
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error checking if image needs update: {ex.Message}");
                            }
                            
                            if (imageItem == null)
                            {
                                imageItem = CreateImageItem(file);
                                if (imageItem != null)
                                {
                                    imageItem.FileHash = _databaseService.CalculateFileHash(file);
                                    itemsToSave.Add(imageItem);
                                }
                            }
                            
                            if (imageItem != null)
                            {
                                allItems.Add(imageItem);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Directory does not exist: {dir}");
                    }
                    
                    System.Threading.Interlocked.Increment(ref processedDirectories);
                    int progress = 40 + (int)((double)processedDirectories / Math.Max(totalDirectories, 1) * 50);
                    app?.Dispatcher.Invoke(() => app.UpdateSplashScreen($"正在扫描目录: {Path.GetFileName(dir)}", progress));
                });
            });
            LoggingHelper.Log("ImageDataProvider.LoadAsync parallel scan finished.");

            if (!itemsToSave.IsEmpty)
            {
                LoggingHelper.Log($"ImageDataProvider.LoadAsync saving {itemsToSave.Count} new/updated images to database.");
                await _databaseService.SaveImagesAsync(itemsToSave);
                LoggingHelper.Log("ImageDataProvider.LoadAsync images saved to database.");
            }
            
            AllImageItems = new ReadOnlyCollection<ImageItem>(allItems.ToList());
            app?.UpdateSplashScreen("图像加载完成", 90);
            LoggingHelper.Log("ImageDataProvider.LoadAsync finished.");
        }

        private bool NeedsUpdate(string filePath, ImageItem dbImage)
        {
            var lastWriteTime = File.GetLastWriteTime(filePath);
            var dbLastModified = DateTime.Parse(dbImage.LastModified ?? "");
            return lastWriteTime > dbLastModified;
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