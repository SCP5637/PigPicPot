using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;

using PigPicPot.Helpers;
using PigPicPot.Messaging;
using PigPicPot.Models;
using PigPicPot.ViewModels;

namespace PigPicPot.Services
{
    /// <summary>
    /// 收藏夹服务，负责管理收藏夹相关功能
    /// Favorites service, responsible for managing favorites related functions
    /// </summary>
    public class FavoriteService
    {
        private FavoritesData _favoritesData = new FavoritesData();
        private readonly string _favoritesFilePath;
        private MainViewModel? _viewModel;
        private IMessenger? _messenger;

        /// <summary>
        /// 构造函数
        /// Constructor
        /// </summary>
        /// <param name="favoritesFilePath">收藏夹文件路径</param>
        public FavoriteService(string favoritesFilePath)
        {
            _favoritesFilePath = favoritesFilePath;
        }

        /// <summary>
        /// 设置视图模型
        /// Set view model
        /// </summary>
        /// <param name="viewModel">视图模型</param>
        public void SetViewModel(MainViewModel viewModel)
        {
            _viewModel = viewModel;
            _messenger = viewModel.Messenger;
        }

        /// <summary>
        /// 加载收藏夹
        /// Load favorites
        /// </summary>
        public void LoadFavorites()
        {
            LoggingHelper.Log("Loading favorites...");
            if (File.Exists(_favoritesFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_favoritesFilePath);
                    _favoritesData = JsonSerializer.Deserialize<FavoritesData>(json) ?? new FavoritesData();
                    LoggingHelper.Log($"Favorites loaded, count: {_favoritesData.Favorites.Count}");

                    bool favoritesModified = false;
                    foreach (var favorite in _favoritesData.Favorites)
                    {
                        foreach (var image in favorite.Images)
                        {
                            if (!File.Exists(image.FilePath))
                            {
                                var foundImage = FindImageByHash(image.Hash);
                                if (foundImage != null)
                                {
                                    image.FilePath = foundImage.FilePath;
                                    favoritesModified = true;
                                }
                            }
                        }
                    }

                    if (favoritesModified)
                    {
                        SaveFavorites();
                    }
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogException(ex, "Error loading favorites");
                }
            }
            else
            {
                // 如果收藏夹文件不存在，创建默认收藏夹
                _favoritesData = new FavoritesData
                {
                    Favorites = new List<Favorite>
                    {
                        new Favorite
                        {
                            Name = "Default",
                            IsDeletable = false
                        }
                    }
                };
                SaveFavorites();
            }

            // 更新视图模型中的收藏夹
            _viewModel?.UpdateFavorites(_favoritesData.Favorites);
            LoggingHelper.Log("Favorites loaded successfully.");
        }

        private ImageItem? FindImageByHash(string hash)
        {
            if (_viewModel == null) return null;

            foreach (var imageItem in _viewModel.AllImages)
            {
                string itemHash = ComputeFileHash(imageItem.FilePath);
                if (itemHash == hash)
                {
                    return imageItem;
                }
            }
            return null;
        }

        /// <summary>
        /// 保存收藏夹
        /// Save favorites
        /// </summary>
        public void SaveFavorites()
        {
            LoggingHelper.Log("Saving favorites...");
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_favoritesData, options);
                File.WriteAllText(_favoritesFilePath, json);
                LoggingHelper.Log("Favorites saved successfully.");
                
                // 通知迷你窗口更新收藏夹
                _messenger?.Send(new FavoritesUpdatedMessage());
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Failed to save favorites");
                LoggingHelper.LogException(ex, "Failed to save favorites");
            }
        }

        /// <summary>
        /// 获取收藏夹数据
        /// Get favorites data
        /// </summary>
        /// <returns>收藏夹数据</returns>
        public FavoritesData GetFavoritesData()
        {
            return _favoritesData;
        }

        /// <summary>
        /// 创建新收藏夹
        /// Create new favorite
        /// </summary>
        /// <param name="name">收藏夹名称</param>
        public void CreateNewFavorite(string name)
        {
            if (!string.IsNullOrWhiteSpace(name) && _favoritesData.Favorites.All(f => f.Name != name))
            {
                var newFavorite = new Favorite
                {
                    Name = name,
                    IsDeletable = true
                };
                _favoritesData.Favorites.Add(newFavorite);
                SaveFavorites();
                _viewModel?.UpdateFavorites(_favoritesData.Favorites);
                LoggingHelper.Log($"New favorite '{name}' created.");
            }
        }

        /// <summary>
        /// 删除收藏夹
        /// Delete favorite
        /// </summary>
        /// <param name="favorite">要删除的收藏夹</param>
        public void DeleteFavorite(Favorite favorite)
        {
            if (favorite.IsDeletable)
            {
                _favoritesData.Favorites.Remove(favorite);
                SaveFavorites();
                _viewModel?.UpdateFavorites(_favoritesData.Favorites);
                LoggingHelper.Log($"Favorite '{favorite.Name}' deleted.");
            }
        }

        /// <summary>
        /// 为图片添加到收藏夹
        /// Add image to favorite
        /// </summary>
        /// <param name="favorite">收藏夹</param>
        /// <param name="imageItem">图片项</param>
        public void AddImageToFavorite(Favorite favorite, ImageItem imageItem)
        {
            if (!favorite.Images.Any(img => img.FilePath == imageItem.FilePath))
            {
                var favoriteImage = new FavoriteImage
                {
                    FilePath = imageItem.FilePath,
                    FileName = imageItem.FileName,
                    Hash = ComputeFileHash(imageItem.FilePath)
                };
                favorite.Images.Add(favoriteImage);
                SaveFavorites();
                LoggingHelper.Log($"Image '{imageItem.FileName}' added to favorite '{favorite.Name}'.");
            }
            else
            {
                LoggingHelper.Log($"Image '{imageItem.FileName}' already exists in favorite '{favorite.Name}'.");
            }
        }

        /// <summary>
        /// 从收藏夹移除图片
        /// Remove image from favorite
        /// </summary>
        /// <param name="favorite">收藏夹</param>
        /// <param name="imageItem">图片项</param>
        public void RemoveImageFromFavorite(Favorite favorite, ImageItem imageItem)
        {
            var imageToRemove = favorite.Images.FirstOrDefault(img => img.FilePath == imageItem.FilePath);
            if (imageToRemove != null)
            {
                favorite.Images.Remove(imageToRemove);
                SaveFavorites();
                LoggingHelper.Log($"Image '{imageItem.FileName}' removed from favorite '{favorite.Name}'.");
            }
        }

        /// <summary>
        /// 计算文件哈希值
        /// Compute file hash
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>文件哈希值</returns>
        public string ComputeFileHash(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// 重命名收藏夹
        /// Rename favorite
        /// </summary>
        /// <param name="favorite">收藏夹</param>
        /// <param name="newName">新名称</param>
        /// <returns>是否成功重命名</returns>
        public bool RenameFavorite(Favorite favorite, string newName)
        {
            if (_favoritesData.Favorites.All(f => f.Name != newName))
            {
                string oldName = favorite.Name;
                favorite.Name = newName;
                SaveFavorites();
                LoggingHelper.Log($"Favorite renamed from '{oldName}' to '{newName}'");
                return true;
            }
            return false;
        }
    }
}