using PigPicPot.Models;

namespace PigPicPot.Services
{
    public interface IThumbnailService
    {
        void QueueThumbnailRequest(ImageItem item);
        void Prioritize(System.Collections.Generic.IEnumerable<ImageItem> highPriorityItems, System.Collections.Generic.IEnumerable<ImageItem> allItems);
    }
}
