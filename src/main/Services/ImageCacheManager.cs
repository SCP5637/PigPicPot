using System;
using System.Collections.Generic;
using System.Linq;
using PigPicPot.Models;
using PigPicPot.Services;

namespace PigPicPot.Services
{
    /// <summary>
    /// 图片缓存管理器，用于管理图片缩略图的内存缓存
    /// Image cache manager for managing thumbnail memory cache
    /// </summary>
    public class ImageCacheManager
    {
        private class CacheEntry
        {
            public ImageItem ImageItem { get; }
            public DateTime LastAccessed { get; set; }
            
            public CacheEntry(ImageItem imageItem)
            {
                ImageItem = imageItem;
                LastAccessed = DateTime.Now;
            }
        }
        
        // 缓存字典，以文件路径为键
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        
        // 缓存大小限制（以MB为单位），-1表示无限
        private int _maxCacheSizeMB = -1;
        
        // 当前缓存大小（以字节为单位）
        private long _currentCacheSize = 0;
        
        // 最大缓存项数量，-1表示无限
        private int _maxCacheItems = -1;
        
        public ImageCacheManager()
        {
            // 从配置文件读取缓存限制设置
            LoadCacheSettings();
        }
        
        /// <summary>
        /// 从配置文件加载缓存设置
        /// Load cache settings from configuration
        /// </summary>
        private void LoadCacheSettings()
        {
            try
            {
                var configService = new ConfigurationService();
                var config = configService.GetConfig();
                
                // 读取最大缓存大小设置，默认为-1（无限）
                if (config.TryGetValue("max_cache_size_mb", out var maxCacheSizeStr))
                {
                    if (int.TryParse(maxCacheSizeStr, out int maxCacheSize))
                    {
                        _maxCacheSizeMB = maxCacheSize;
                    }
                }
                
                // 读取最大缓存项数量设置，默认为-1（无限）
                if (config.TryGetValue("max_cache_items", out var maxCacheItemsStr))
                {
                    if (int.TryParse(maxCacheItemsStr, out int maxCacheItems))
                    {
                        _maxCacheItems = maxCacheItems;
                    }
                }
            }
            catch (Exception)
            {
                // 如果读取配置失败，使用默认值（无限）
                _maxCacheSizeMB = -1;
                _maxCacheItems = -1;
            }
        }
        
        /// <summary>
        /// 尝试从缓存获取图片缩略图
        /// Try to get image thumbnail from cache
        /// </summary>
        /// <param name="imageItem">图片项</param>
        /// <returns>如果找到返回true，否则返回false</returns>
        public bool TryGetCachedThumbnail(ImageItem imageItem)
        {
            if (_cache.TryGetValue(imageItem.FilePath, out var entry))
            {
                // 更新最后访问时间
                entry.LastAccessed = DateTime.Now;
                
                // 如果图片项还没有缩略图，但缓存中有，则赋值
                if (imageItem.ThumbnailSource == null && entry.ImageItem.ThumbnailSource != null)
                {
                    imageItem.ThumbnailSource = entry.ImageItem.ThumbnailSource;
                }
                
                return imageItem.ThumbnailSource != null;
            }
            
            return false;
        }
        
        /// <summary>
        /// 将图片缩略图添加到缓存
        /// Add image thumbnail to cache
        /// </summary>
        /// <param name="imageItem">图片项</param>
        public void AddToCache(ImageItem imageItem)
        {
            if (imageItem.ThumbnailSource == null)
                return;
            
            // 检查是否需要清理缓存
            CleanupCacheIfNeeded();
            
            // 添加到缓存
            if (!_cache.ContainsKey(imageItem.FilePath))
            {
                _cache[imageItem.FilePath] = new CacheEntry(imageItem);
                _currentCacheSize += EstimateThumbnailSize(imageItem);
            }
        }
        
        /// <summary>
        /// 从缓存中移除图片
        /// Remove image from cache
        /// </summary>
        /// <param name="imageItem">图片项</param>
        public void RemoveFromCache(ImageItem imageItem)
        {
            if (_cache.TryGetValue(imageItem.FilePath, out var entry))
            {
                _currentCacheSize -= EstimateThumbnailSize(entry.ImageItem);
                _cache.Remove(imageItem.FilePath);
                
                // 释放图片项的缩略图资源
                imageItem.ThumbnailSource = null;
            }
        }
        
        /// <summary>
        /// 清理缓存如果需要
        /// Clean up cache if needed
        /// </summary>
        private void CleanupCacheIfNeeded()
        {
            // 检查缓存项数量（如果限制不为-1）
            if (_maxCacheItems != -1 && _cache.Count >= _maxCacheItems)
            {
                RemoveLeastRecentlyUsedItems();
            }
            
            // 检查缓存大小（如果限制不为-1）
            if (_maxCacheSizeMB != -1 && _currentCacheSize > _maxCacheSizeMB * 1024L * 1024L)
            {
                RemoveLeastRecentlyUsedItems();
            }
        }
        
        /// <summary>
        /// 移除最近最少使用的项
        /// Remove least recently used items
        /// </summary>
        private void RemoveLeastRecentlyUsedItems()
        {
            // 找出最近最少使用的项
            var itemsToRemove = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Take(_cache.Count / 4) // 移除四分之一的项
                .ToList();
            
            // 移除这些项
            foreach (var kvp in itemsToRemove)
            {
                _currentCacheSize -= EstimateThumbnailSize(kvp.Value.ImageItem);
                kvp.Value.ImageItem.ThumbnailSource = null; // 释放资源
                _cache.Remove(kvp.Key);
            }
        }
        
        /// <summary>
        /// 估算缩略图大小（以字节为单位）
        /// Estimate thumbnail size in bytes
        /// </summary>
        /// <param name="imageItem">图片项</param>
        /// <returns>估算的大小</returns>
        private long EstimateThumbnailSize(ImageItem imageItem)
        {
            // 这是一个粗略的估算
            // 假设缩略图大约是 150x150 像素，每个像素4字节（32位色）
            return 150L * 150L * 4L;
        }
        
        /// <summary>
        /// 清空缓存
        /// Clear cache
        /// </summary>
        public void ClearCache()
        {
            foreach (var entry in _cache.Values)
            {
                // 确保释放缩略图资源
                if (entry.ImageItem.ThumbnailSource != null)
                {
                    entry.ImageItem.ThumbnailSource = null;
                }
            }
            _cache.Clear();
            _currentCacheSize = 0;
        }
    }
}