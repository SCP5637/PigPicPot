using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using PigPicPot.Models;

namespace PigPicPot.Services
{
    public class ThumbnailService : IThumbnailService
    {
        private BlockingCollection<ImageItem> _thumbnailQueue = new BlockingCollection<ImageItem>();
        private readonly ImageProcessingService _imageProcessingService;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processingTask;

        public ThumbnailService()
        {
            _imageProcessingService = new ImageProcessingService();
            _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
        }

        public void QueueThumbnailRequest(ImageItem item)
        {
            if (!item.IsThumbnailQueued && item.ThumbnailSource == null && !_thumbnailQueue.IsAddingCompleted)
            {
                item.IsThumbnailQueued = true;
                _thumbnailQueue.Add(item);
            }
        }

        public void Prioritize(IEnumerable<ImageItem> highPriorityItems, IEnumerable<ImageItem> allItems)
        {
            // 1. Cancel the old task
            _cts.Cancel();
            
            // 2. Reset the state
            _cts = new CancellationTokenSource();
            
            // Get a list of items that were in the old queue but not processed
            var unprocessedItems = _thumbnailQueue.ToList();
            foreach(var item in unprocessedItems)
            {
                item.IsThumbnailQueued = false;
            }

            _thumbnailQueue = new BlockingCollection<ImageItem>();

            // 3. Queue the new high-priority items first
            var highPriorityList = highPriorityItems
                .Where(i => i.ThumbnailSource == null && !i.IsCorrupted)
                .ToList();

            foreach (var item in highPriorityList)
            {
                QueueThumbnailRequest(item);
            }

            // 4. Queue the rest of the items
            var highPrioritySet = new HashSet<ImageItem>(highPriorityList);
            var remainingItems = allItems
                .Where(i => i.ThumbnailSource == null && !i.IsCorrupted && !highPrioritySet.Contains(i));
            
            foreach (var item in remainingItems)
            {
                QueueThumbnailRequest(item);
            }

            // 5. Start the new processing task
            _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
        }

        private async Task ProcessThumbnailQueue(CancellationToken token)
        {
            try
            {
                foreach (var item in _thumbnailQueue.GetConsumingEnumerable(token))
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        bool isOk = true;
                        if (item.IsAnimated)
                        {
                            isOk = await _imageProcessingService.RepairGifAsync(item.FilePath);
                        }

                        if (!isOk)
                        {
                            item.IsThumbnailQueued = false;
                            item.IsCorrupted = true;
                            continue;
                        }

                        var thumbnail = GenerateThumbnail(item);
                        if (thumbnail != null)
                        {
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                item.ThumbnailSource = thumbnail;
                            });
                        }
                        else
                        {
                            item.IsThumbnailQueued = false;
                            item.IsCorrupted = true;
                        }
                    }
                    catch (System.Exception ex)
                    {
                        System.Console.WriteLine($"Error processing queue for {item.FileName}: {ex.Message}");
                        item.IsThumbnailQueued = false;
                        item.IsCorrupted = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // This is expected when prioritization happens.
            }
        }

        private BitmapSource? GenerateThumbnail(ImageItem item)
        {
            try
            {
                var bitmap = new BitmapImage();
                using (var bitmapStream = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 150;
                    bitmap.StreamSource = bitmapStream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                return bitmap;
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}