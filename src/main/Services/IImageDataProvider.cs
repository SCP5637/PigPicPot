using System.Collections.ObjectModel;
using System.Threading.Tasks;
using PigPicPot.Models;

namespace PigPicPot.Services
{
    public interface IImageDataProvider
    {
        ReadOnlyCollection<ImageItem> AllImageItems { get; }
        Task LoadAsync(System.Collections.Generic.IEnumerable<string> directoriesToScan);
    }
}
