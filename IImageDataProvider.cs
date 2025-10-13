using System.Collections.ObjectModel;
using System.Threading.Tasks;

public interface IImageDataProvider
{
    ReadOnlyCollection<ImageItem> AllImageItems { get; }
    Task LoadAsync(IEnumerable<string> directoriesToScan);
}