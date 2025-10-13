using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows;

public class ThumbnailService : IThumbnailService
{
    private readonly BlockingCollection<ImageItem> _thumbnailQueue = new BlockingCollection<ImageItem>();
    private readonly ImageProcessingService _imageProcessingService;

    public ThumbnailService()
    {
        _imageProcessingService = new ImageProcessingService();
        Task.Run(() => ProcessThumbnailQueue());
    }

    public void QueueThumbnailRequest(ImageItem item)
    {
        if (!item.IsThumbnailQueued)
        {
            item.IsThumbnailQueued = true;
            _thumbnailQueue.Add(item);
        }
    }

    private async Task ProcessThumbnailQueue()
    {
        foreach (var item in _thumbnailQueue.GetConsumingEnumerable())
        {
            try
            {
                bool isOk = true;
                // If it's a GIF, try to repair it first.
                if (item.IsAnimated)
                {
                    isOk = await _imageProcessingService.RepairGifAsync(item.FilePath);
                }

                if (!isOk)
                {
                    // Repair failed, mark as corrupted and skip thumbnail.
                    item.IsThumbnailQueued = false;
                    item.IsCorrupted = true;
                    System.Console.WriteLine($"Failed to repair or validate GIF for {item.FileName}");
                    continue; // Move to the next item in the queue
                }

                // If repair was successful (or not needed), generate the thumbnail.
                var thumbnail = GenerateThumbnail(item);
                if (thumbnail != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.ThumbnailSource = thumbnail;
                    });
                }
                else
                {
                    // Thumbnail generation failed even after potential repair.
                    item.IsThumbnailQueued = false;
                    item.IsCorrupted = true;
                    System.Console.WriteLine($"Failed to generate thumbnail for {item.FileName}");
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

    private BitmapSource? GenerateThumbnail(ImageItem item)
    {
        try
        {
            // We no longer need ImageSharp validation here, as the repair step handles it.
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
        catch (System.Exception ex)
        {
            System.Console.WriteLine($"Thumbnail generation failed for {item.FileName}: {ex.Message}");
            return null;
        }
    }
}