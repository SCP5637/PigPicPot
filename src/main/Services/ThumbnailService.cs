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
    /// <summary>
    /// 缩略图服务，负责生成和管理图像缩略图
    /// Thumbnail service, responsible for generating and managing image thumbnails
    /// </summary>
    public class ThumbnailService : IThumbnailService
    {
        private BlockingCollection<ImageItem> _thumbnailQueue = new BlockingCollection<ImageItem>();
        private readonly ImageProcessingService _imageProcessingService;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task _processingTask;

        /// <summary>
        /// 构造函数，初始化图像处理服务并启动处理任务
        /// Constructor, initialize image processing service and start processing task
        /// </summary>
        public ThumbnailService()
        {
            _imageProcessingService = new ImageProcessingService();
            _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
        }

        /// <summary>
        /// 将图像项加入缩略图生成队列
        /// Queue image item for thumbnail generation
        /// </summary>
        /// <param name="item">需要生成缩略图的图像项</param>
        public void QueueThumbnailRequest(ImageItem item)
        {
            if (!item.IsThumbnailQueued && item.ThumbnailSource == null && !_thumbnailQueue.IsAddingCompleted)
            {
                item.IsThumbnailQueued = true;
                _thumbnailQueue.Add(item);
            }
        }

        /// <summary>
        /// 设置高优先级图像项
        /// Set high priority image items
        /// </summary>
        /// <param name="highPriorityItems">高优先级图像项集合</param>
        /// <param name="allItems">所有图像项集合</param>
        public void Prioritize(IEnumerable<ImageItem> highPriorityItems, IEnumerable<ImageItem> allItems)
        {
            _cts.Cancel();
            
            _cts = new CancellationTokenSource();
            
            var unprocessedItems = _thumbnailQueue.ToList();
            foreach(var item in unprocessedItems)
            {
                item.IsThumbnailQueued = false;
            }

            _thumbnailQueue = new BlockingCollection<ImageItem>();

            var highPriorityList = highPriorityItems
                .Where(i => i.ThumbnailSource == null && !i.IsCorrupted)
                .ToList();

            foreach (var item in highPriorityList)
            {
                QueueThumbnailRequest(item);
            }

            var highPrioritySet = new HashSet<ImageItem>(highPriorityList);
            var remainingItems = allItems
                .Where(i => i.ThumbnailSource == null && !i.IsCorrupted && !highPrioritySet.Contains(i));
            
            foreach (var item in remainingItems)
            {
                QueueThumbnailRequest(item);
            }

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