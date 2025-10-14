using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PigPicPot.Models;

namespace PigPicPot.Services
{
    public interface ITagProvider
    {
        ReadOnlyCollection<TagNode> RootTags { get; }
        ReadOnlyCollection<ImageItem> AllImageItems { get; }
        Task LoadAsync();
    }
}
