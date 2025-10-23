using PigPicPot.Models;
using System.Collections.Generic;

namespace PigPicPot.Services
{
    public interface IThumbnailService
    {
        void QueueThumbnailRequest(ImageItem item);
        void Prioritize(IEnumerable<ImageItem> highPriorityItems, IEnumerable<ImageItem> allItems);
        void SetAllItems(IEnumerable<ImageItem> allItems);
        void SetVisibleItems(IEnumerable<ImageItem> visibleItems);
    }
}