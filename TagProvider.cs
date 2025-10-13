
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

public class TagProvider : ITagProvider
{
    private readonly IImageDataProvider _imageDataProvider;
    public ReadOnlyCollection<TagNode> RootTags { get; private set; } = new ReadOnlyCollection<TagNode>(new List<TagNode>());
    public ReadOnlyCollection<ImageItem> AllImageItems => _imageDataProvider.AllImageItems;

    public TagProvider()
    {
        // In a real DI scenario, this would be injected.
        // For now, we still create it here.
        _imageDataProvider = new ImageDataProvider();
    }

    public async Task LoadAsync()
    {
        await Task.Run(async () =>
        {
            // 1. Build the complete directory tree and find leaf directories.
            var rootTags = new List<TagNode>();
            var leafDirectories = new List<string>();
            var resourcePath = Path.Combine(PathHelper.GetApplicationRoot(), "resource");

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

            // 2. Load images ONLY from the leaf directories.
            await _imageDataProvider.LoadAsync(leafDirectories);
            var allImageItems = _imageDataProvider.AllImageItems;

            // 3. Add dynamic filename-based tags to the leaf nodes in the tree.
            AddDynamicTagsToLeaves(this.RootTags, allImageItems);
        });
    }

    private TagNode? BuildTagTree(string directoryPath, int level, List<string> leafDirectories)
    {
        var dirName = Path.GetFileName(directoryPath);
        if (string.IsNullOrEmpty(dirName) || dirName.Equals("temp", System.StringComparison.OrdinalIgnoreCase))
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
            // This is a leaf directory, add its path to the list for image scanning.
            leafDirectories.Add(directoryPath);
        }
        else
        {
            // This is not a leaf, so recurse into its children.
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
            // Recurse to the deepest nodes first
            if (tag.Children.Any())
            {
                AddDynamicTagsToLeaves(tag.Children, allImageItems);
            }
            
            // Check if the current tag is a leaf in the *directory* structure.
            // A tag is a directory leaf if it originally had no children before dynamic tags were added.
            // We can infer this by checking if its children are all series tags (or if it has no children yet).
            if (!tag.Children.Any() || tag.Children.All(c => c.IsSeriesTag))
            {
                // Reconstruct the full path for this tag.
                var pathParts = new Stack<string>();
                var current = tag;
                while (current != null)
                {
                    pathParts.Push(current.DirectoryName);
                    current = current.Parent;
                }
                // This creates a relative path from the 'resource' folder
                var relativeTagPath = Path.Combine(pathParts.ToArray());
                var absoluteTagPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "resource", relativeTagPath);

                var filesInThisDir = allImageItems.Where(item =>
                    Path.GetDirectoryName(item.FilePath)?.Equals(absoluteTagPath, System.StringComparison.OrdinalIgnoreCase) == true
                ).ToList();

                if (filesInThisDir.Any())
                {
                    // Same logic as before to group by name and create series tags
                    var chineseNameGroups = filesInThisDir
                        .Where(item => !string.IsNullOrEmpty(item.BaseChineseName))
                        .GroupBy(item => item.BaseChineseName)
                        .ToList();

                    var variantGroups = chineseNameGroups.Where(g => g.Count() > 1 || g.Any(i => i.HasVariant)).ToList();
                    var otherGroups = chineseNameGroups.Except(variantGroups).ToList();

                    foreach (var group in variantGroups)
                    {
                        var seriesTag = new TagNode
                        {
                            DirectoryName = group.Key,
                            DisplayName = group.Key,
                            Level = tag.Level + 1,
                            IsSelected = false,
                            IsSeriesTag = true,
                            Parent = tag!
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
                            Parent = tag!
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
