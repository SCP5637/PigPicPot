using System.Collections.ObjectModel;
using System.Threading.Tasks;

public interface ITagProvider
{
    ReadOnlyCollection<TagNode> RootTags { get; }
    ReadOnlyCollection<ImageItem> AllImageItems { get; }
    Task LoadAsync();
}