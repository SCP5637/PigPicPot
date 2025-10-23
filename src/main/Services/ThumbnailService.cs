using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using PigPicPot.Models;
using PigPicPot.Helpers;

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
        private readonly ImageDatabaseService _databaseService;
        private readonly ImageCacheManager _cacheManager;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private Task? _processingTask;
        // 添加一个字典来跟踪正在处理的项目，避免重复处理
        private readonly HashSet<string> _processingItems = new HashSet<string>();
        private IEnumerable<ImageItem>? _allItems;
        private int _maxConcurrentProcessing = 2; // 限制并发处理数量
        
        // 跟踪当前已加载的图片项
        private readonly HashSet<string> _loadedItems = new HashSet<string>();

        /// <summary>
        /// 构造函数，初始化图像处理服务并启动处理任务
        /// Constructor, initialize image processing service and start processing task
        /// </summary>
        public ThumbnailService()
        {
            _imageProcessingService = new ImageProcessingService();
            _databaseService = new ImageDatabaseService();
            _cacheManager = new ImageCacheManager();
            // 不再在初始化时启动处理任务，改为按需处理
            _processingTask = null;
        }

        /// <summary>
        /// 设置可见的图像项，优先处理这些项
        /// Set visible image items to prioritize processing
        /// </summary>
        /// <param name="visibleItems">可见的图像项集合</param>
        public void SetVisibleItems(IEnumerable<ImageItem> visibleItems)
        {
            // 取消当前所有处理任务
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            
            var unprocessedItems = _thumbnailQueue.ToList();
            foreach(var item in unprocessedItems)
            {
                item.IsThumbnailQueued = false;
                lock (_processingItems)
                {
                    _processingItems.Remove(item.FilePath);
                }
            }

            _thumbnailQueue = new BlockingCollection<ImageItem>();
            
            var visibleItemsSet = new HashSet<string>(visibleItems.Select(i => i.FilePath));
            
            // 卸载不可见的图片项
            var itemsToUnload = _loadedItems.Except(visibleItemsSet).ToList();
            foreach (var filePath in itemsToUnload)
            {
                var item = _allItems?.FirstOrDefault(i => i.FilePath == filePath);
                if (item != null)
                {
                    RemoveFromCache(item);
                }
            }
            
            // 更新已加载项列表
            foreach (var item in visibleItems)
            {
                _loadedItems.Add(item.FilePath);
            }
            
            // 为可见项添加到处理队列（但不立即处理）
            foreach (var item in visibleItems.Where(i => i.ThumbnailSource == null && !i.IsCorrupted))
            {
                if (!item.IsThumbnailQueued && !_thumbnailQueue.IsAddingCompleted)
                {
                    lock (_processingItems)
                    {
                        // 避免重复添加到队列中
                        if (_processingItems.Contains(item.FilePath))
                            continue;
                        
                        _processingItems.Add(item.FilePath);
                    }
                    
                    item.IsThumbnailQueued = true;
                    _thumbnailQueue.Add(item);
                }
            }
            
            // 重新启动处理任务
            if (_processingTask == null || _processingTask.IsCompleted)
            {
                _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
            }
        }

        /// <summary>
        /// 设置所有图像项
        /// </summary>
        /// <param name="allItems">所有图像项</param>
        public void SetAllItems(IEnumerable<ImageItem> allItems)
        {
            _allItems = allItems;
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
                lock (_processingItems)
                {
                    // 避免重复添加到队列中
                    if (_processingItems.Contains(item.FilePath))
                        return;
                    
                    _processingItems.Add(item.FilePath);
                }
                
                item.IsThumbnailQueued = true;
                _thumbnailQueue.Add(item);
                
                // 启动处理任务（如果尚未启动）
                if (_processingTask == null || _processingTask.IsCompleted)
                {
                    _cts = new CancellationTokenSource();
                    _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
                }
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
                lock (_processingItems)
                {
                    _processingItems.Remove(item.FilePath);
                }
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
            
            // 只将前50个非高优先级项加入队列，避免一次性加载太多
            var limitedRemainingItems = remainingItems.Take(50);
            
            foreach (var item in limitedRemainingItems)
            {
                QueueThumbnailRequest(item);
            }

            // 启动处理任务（如果尚未启动）
            if (_processingTask == null || _processingTask.IsCompleted)
            {
                _processingTask = Task.Run(() => ProcessThumbnailQueue(_cts.Token));
            }
        }

        private async Task ProcessThumbnailQueue(CancellationToken token)
        {
            try
            {
                var semaphore = new SemaphoreSlim(_maxConcurrentProcessing, _maxConcurrentProcessing);
                
                var tasks = new List<Task>();
                
                foreach (var item in _thumbnailQueue.GetConsumingEnumerable(token))
                {
                    token.ThrowIfCancellationRequested();
                    
                    await semaphore.WaitAsync(token);
                    
                    var task = Task.Run(async () =>
                    {
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
                                lock (_processingItems)
                                {
                                    _processingItems.Remove(item.FilePath);
                                }
                                return;
                            }

                            var thumbnail = GenerateThumbnail(item);
                            if (thumbnail != null)
                            {
                                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                                {
                                    item.ThumbnailSource = thumbnail;
                                });
                                
                                // 保存缩略图到数据库
                                try 
                                {
                                    await _databaseService.SaveImageAsync(item);
                                }
                                catch (Exception ex)
                                {
                                    // 捕获数据库保存错误，防止中断整个处理流程
                                    LoggingHelper.LogException(ex, $"Failed to save image to database: {item.FilePath}");
                                }
                                
                                // 添加到缓存
                                _cacheManager.AddToCache(item);
                                // 标记为已加载
                                _loadedItems.Add(item.FilePath);
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
                        finally
                        {
                            // 确保从处理集合中移除
                            lock (_processingItems)
                            {
                                _processingItems.Remove(item.FilePath);
                            }
                            semaphore.Release();
                        }
                    });
                    
                    tasks.Add(task);
                }
                
                // 等待所有任务完成
                await Task.WhenAll(tasks);
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
        
        /// <summary>
        /// 从数据库加载缩略图
        /// Load thumbnail from database
        /// </summary>
        /// <param name="item">图像项</param>
        /// <returns>异步任务</returns>
        public async Task LoadThumbnailFromDatabaseAsync(ImageItem item)
        {
            // 首先尝试从内存缓存获取
            if (_cacheManager.TryGetCachedThumbnail(item))
            {
                // 标记为已加载
                _loadedItems.Add(item.FilePath);
                return;
            }
            
            // 如果缓存中没有，则从数据库加载
            if (item.ThumbnailSource == null)
            {
                var thumbnail = await _databaseService.LoadThumbnailAsync(item.FilePath);
                if (thumbnail != null)
                {
                    item.ThumbnailSource = thumbnail;
                    // 添加到缓存
                    _cacheManager.AddToCache(item);
                    // 标记为已加载
                    _loadedItems.Add(item.FilePath);
                }
            }
        }
        
        /// <summary>
        /// 从缓存中移除图片项
        /// Remove image item from cache
        /// </summary>
        /// <param name="item">图片项</param>
        public void RemoveFromCache(ImageItem item)
        {
            _cacheManager.RemoveFromCache(item);
            // 同时清空缩略图源以释放内存
            item.ThumbnailSource = null;
            // 从已加载项中移除
            _loadedItems.Remove(item.FilePath);
            // 从处理队列中移除（如果存在）
            lock (_processingItems)
            {
                _processingItems.Remove(item.FilePath);
            }
            
            // 强制进行垃圾回收以释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        
        /// <summary>
        /// 清空所有缓存和加载的图片
        /// Clear all cache and loaded images
        /// </summary>
        public void ClearAll()
        {
            // 取消所有正在进行的任务
            _cts.Cancel();
            _cts = new CancellationTokenSource();
            
            // 清空处理队列
            var unprocessedItems = _thumbnailQueue.ToList();
            foreach(var item in unprocessedItems)
            {
                item.IsThumbnailQueued = false;
            }
            
            _thumbnailQueue = new BlockingCollection<ImageItem>();
            
            // 清空缓存
            _cacheManager.ClearCache();
            
            // 清空已加载项跟踪列表
            _loadedItems.Clear();
            
            // 重置处理任务
            _processingTask = null;
            
            // 清理处理项目集合
            lock (_processingItems)
            {
                _processingItems.Clear();
            }
            
            // 强制进行垃圾回收以释放内存
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}