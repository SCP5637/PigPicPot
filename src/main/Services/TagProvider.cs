using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using PigPicPot.Models;
using PigPicPot.Helpers;
using System.Reflection;
using System.Diagnostics;
using PigPicPot.Views;

namespace PigPicPot.Services
{
    /// <summary>
    /// 标签提供者，负责构建和管理标签树结构
    /// Tag provider, responsible for building and managing tag tree structure
    /// </summary>
    public class TagProvider : ITagProvider
    {
        private readonly IImageDataProvider _imageDataProvider;
        
        /// <summary>
        /// 根标签集合
        /// Root tags collection
        /// </summary>
        public ReadOnlyCollection<TagNode> RootTags { get; private set; } = new ReadOnlyCollection<TagNode>(new List<TagNode>());
        
        /// <summary>
        /// 所有图片项集合
        /// All image items collection
        /// </summary>
        public ReadOnlyCollection<ImageItem> AllImageItems => _imageDataProvider.AllImageItems;

        /// <summary>
        /// 构造函数，初始化图像数据提供者
        /// Constructor, initialize image data provider
        /// </summary>
        public TagProvider()
        {
            _imageDataProvider = new ImageDataProvider();
        }

        /// <summary>
        /// 异步加载标签和图像数据
        /// Asynchronously load tags and image data
        /// </summary>
        public async Task LoadAsync()
        {
            LoggingHelper.Log("TagProvider.LoadAsync started.");
            var app = System.Windows.Application.Current as App;
            app?.UpdateSplashScreen("正在初始化标签系统...", 5);
            
            var rootTags = new List<TagNode>();
            var leafDirectories = new List<string>();
            
            string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                ?? Assembly.GetEntryAssembly()?.Location 
                ?? AppDomain.CurrentDomain.BaseDirectory;
            string appDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
            var resourcePath = Path.Combine(appDir, "resource");

            // 构建标签树结构
            // Build tag tree structure
            LoggingHelper.Log("TagProvider.LoadAsync building tag tree.");
            if (Directory.Exists(resourcePath))
            {
                var topLevelDirs = Directory.GetDirectories(resourcePath);
                foreach (var dir in topLevelDirs)
                {
                    var tagNode = BuildTagTree(dir, 1, leafDirectories);
                    if (tagNode != null)
                    {
                        rootTags.Add(tagNode);
                    }
                }
            }
            this.RootTags = new ReadOnlyCollection<TagNode>(rootTags);
            LoggingHelper.Log("TagProvider.LoadAsync tag tree built.");

            // 加载图像数据
            // Load image data
            app?.UpdateSplashScreen("正在加载图像数据...", 15);
            LoggingHelper.Log("TagProvider.LoadAsync loading image data.");
            await _imageDataProvider.LoadAsync(leafDirectories);
            LoggingHelper.Log("TagProvider.LoadAsync image data loaded.");
            var allImageItems = _imageDataProvider.AllImageItems;

            // 添加动态标签到叶子节点
            // Add dynamic tags to leaf nodes
            app?.UpdateSplashScreen("正在构建动态标签...", 90);
            LoggingHelper.Log("TagProvider.LoadAsync adding dynamic tags.");
            AddDynamicTagsToLeaves(this.RootTags, allImageItems);
            LoggingHelper.Log("TagProvider.LoadAsync dynamic tags added.");
            
            // 发送完成消息
            app?.UpdateSplashScreen("标签系统初始化完成", 95);
            LoggingHelper.Log("TagProvider.LoadAsync finished.");
        }

        private TagNode? BuildTagTree(string directoryPath, int level, List<string> leafDirectories)
        {
            var dirName = Path.GetFileName(directoryPath);
            if (string.IsNullOrEmpty(dirName) || dirName.Equals("temp", System.StringComparison.OrdinalIgnoreCase) || dirName.Equals("db", System.StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var tagNode = new TagNode
            {
                DirectoryName = dirName,
                DisplayName = GetDisplayName(dirName),
                Level = level,
                IsSelected = false,
                IsSeriesTag = false
            };

            var subDirs = Directory.GetDirectories(directoryPath);

            if (subDirs.Length == 0)
            {
                leafDirectories.Add(directoryPath);
            }
            else
            {
                foreach (var subDir in subDirs)
                {
                    var childNode = BuildTagTree(subDir, level + 1, leafDirectories);
                    if (childNode != null)
                    {
                        childNode.Parent = tagNode;
                        tagNode.Children.Add(childNode);
                    }
                }
            }

            return tagNode;
        }

        private void AddDynamicTagsToLeaves(IEnumerable<TagNode> tags, ReadOnlyCollection<ImageItem> allImageItems)
        {
            foreach (var tag in tags)
            {
                if (tag.Children.Any())
                {
                    AddDynamicTagsToLeaves(tag.Children, allImageItems);
                }

                // 修改逻辑：始终在叶子节点添加基于文件名的动态标签
                // Modified logic: Always add filename-based dynamic tags at leaf nodes
                if (!tag.Children.Any() || tag.Children.All(c => c.IsSeriesTag))
                {
                    var pathParts = new Stack<string>();
                    var current = tag;
                    while (current != null)
                    {
                        pathParts.Push(current.DirectoryName);
                        current = current.Parent;
                    }
                    var relativeTagPath = Path.Combine(pathParts.ToArray());
                    
                    string exePath = Process.GetCurrentProcess().MainModule?.FileName 
                        ?? Assembly.GetEntryAssembly()?.Location 
                        ?? AppDomain.CurrentDomain.BaseDirectory;
                    string appDir = Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory;
                    var absoluteTagPath = Path.Combine(appDir, "resource", relativeTagPath);

                    var filesInThisDir = allImageItems.Where(item =>
                        Path.GetDirectoryName(item.FilePath ?? string.Empty)?.Equals(absoluteTagPath, System.StringComparison.OrdinalIgnoreCase) == true
                    ).ToList();

                    if (filesInThisDir.Any())
                    {
                        var chineseNameGroups = filesInThisDir
                            .Where(item => !string.IsNullOrEmpty(item.BaseChineseName))
                            .GroupBy(item => item.BaseChineseName!)
                            .ToList();

                        var variantGroups = chineseNameGroups.Where(g => g.Count() > 1 || g.Any(i => i?.HasVariant == true)).ToList();
                        var otherGroups = chineseNameGroups.Except(variantGroups.Cast<IGrouping<string, ImageItem>>(), new GroupEqualityComparer()).ToList();

                        // 清除现有的系列标签
                        // Clear existing series tags
                        var seriesTagsToRemove = tag.Children.Where(c => c.IsSeriesTag).ToList();
                        foreach (var seriesTag in seriesTagsToRemove)
                        {
                            tag.Children.Remove(seriesTag);
                        }

                        // 添加基于文件名的动态标签（始终在最底层）
                        // Add filename-based dynamic tags (always at the bottom level)
                        foreach (var group in variantGroups)
                        {
                            var seriesTag = new TagNode
                            {
                                DirectoryName = group.Key ?? "",
                                DisplayName = group.Key ?? "",
                                Level = tag.Level + 1,
                                IsSelected = false,
                                IsSeriesTag = true,
                                Parent = tag
                            };
                            tag.Children.Add(seriesTag);
                        }

                        if (otherGroups.Any())
                        {
                            var otherTag = new TagNode
                            {
                                DirectoryName = "其他",
                                DisplayName = "其他",
                                Level = tag.Level + 1,
                                IsSelected = false,
                                IsSeriesTag = true,
                                Parent = tag
                            };
                            tag.Children.Add(otherTag);
                        }
                    }
                }
            }
        }

        private string GetDisplayName(string directoryName)
        {
            return directoryName.Replace('_', ' ');
        }
    }

    internal class GroupEqualityComparer : IEqualityComparer<IGrouping<string, ImageItem>>
    {
        public bool Equals(IGrouping<string, ImageItem>? x, IGrouping<string, ImageItem>? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.Key == y.Key;
        }

        public int GetHashCode(IGrouping<string, ImageItem>? obj)
        {
            return obj?.Key.GetHashCode() ?? 0;
        }
    }
}