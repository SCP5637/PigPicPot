using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PigPicPot.Helpers;
using PigPicPot.Models;
using System.Windows.Media.Imaging;
using System.IO.Compression;
using System.Threading;
using System.Security.Cryptography;
using System.Text;

namespace PigPicPot.Services
{
    public class ImageDatabaseService
    {
        private readonly string _databasePath;
        
        public ImageDatabaseService()
        {
            // 数据库文件路径：resource/db/images.db
            // 修复：使用DataRoot而不是AppRoot来定位资源目录
            string dbDirectory = Path.Combine(PathManager.DataRoot, "resource", "db");
            Directory.CreateDirectory(dbDirectory); // 确保目录存在
            _databasePath = Path.Combine(dbDirectory, "images.db");
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            
            // 检查是否需要更新表结构
            CheckAndUpdateTableStructure(connection);
        }
        
        private void CheckAndUpdateTableStructure(SqliteConnection connection)
        {
            // 创建表（如果不存在）
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS images (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    file_path TEXT UNIQUE NOT NULL,
                    file_name TEXT NOT NULL,
                    is_animated INTEGER NOT NULL,
                    series_tag TEXT,
                    base_chinese_name TEXT,
                    variant_number TEXT,
                    has_variant INTEGER NOT NULL,
                    thumbnail_data BLOB,
                    last_modified TEXT NOT NULL
                )";
            command.ExecuteNonQuery();
            
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS image_tags (
                    image_id INTEGER NOT NULL,
                    tag TEXT NOT NULL,
                    FOREIGN KEY(image_id) REFERENCES images(id) ON DELETE CASCADE
                )";
            command.ExecuteNonQuery();
            
            command.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_images_file_path ON images(file_path);
                CREATE INDEX IF NOT EXISTS idx_image_tags_tag ON image_tags(tag);
                CREATE INDEX IF NOT EXISTS idx_image_tags_image_id ON image_tags(image_id);";
            command.ExecuteNonQuery();
            
            // 检查是否需要添加file_hash列
            try
            {
                command.CommandText = "SELECT file_hash FROM images LIMIT 1";
                command.ExecuteScalar();
            }
            catch (SqliteException)
            {
                // 如果出现异常，说明file_hash列不存在，需要添加
                try
                {
                    command.CommandText = "ALTER TABLE images ADD COLUMN file_hash TEXT";
                    command.ExecuteNonQuery();
                }
                catch (SqliteException ex)
                {
                    Console.WriteLine($"Error adding file_hash column: {ex.Message}");
                }
            }
        }
        
        public string CalculateFileHash(string filePath)
        {
            // 如果文件不存在，返回空字符串
            if (!File.Exists(filePath))
                return string.Empty;
                
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        
        public async Task<bool> ImageExistsAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={_databasePath}");
                    connection.Open();
                    
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT COUNT(*) FROM images WHERE file_path = @file_path";
                    command.Parameters.AddWithValue("@file_path", filePath);
                    
                    var result = command.ExecuteScalar();
                    return Convert.ToInt32(result) > 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error checking if image exists in database: {ex.Message}");
                    return false;
                }
            });
        }
        
        public async Task SaveImageAsync(ImageItem imageItem)
        {
            await SaveImagesAsync(new[] { imageItem });
        }

        public async Task SaveImagesAsync(IEnumerable<ImageItem> imageItems)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={_databasePath}");
                    connection.Open();
                    
                    using var transaction = connection.BeginTransaction();
                    
                    foreach (var imageItem in imageItems)
                    {
                        // 生成并压缩缩略图数据
                        byte[]? thumbnailData = null;
                        if (imageItem.ThumbnailSource is BitmapImage bitmapImage)
                        {
                            thumbnailData = ConvertBitmapToBytes(bitmapImage);
                            if (thumbnailData != null)
                            {
                                thumbnailData = Compress(thumbnailData);
                            }
                        }
                        
                        var command = connection.CreateCommand();
                        command.CommandText = @"
                            INSERT OR REPLACE INTO images 
                            (file_path, file_name, is_animated, series_tag, base_chinese_name, variant_number, has_variant, thumbnail_data, last_modified, file_hash)
                            VALUES 
                            (@file_path, @file_name, @is_animated, @series_tag, @base_chinese_name, @variant_number, @has_variant, @thumbnail_data, @last_modified, @file_hash)";
                        
                        command.Parameters.AddWithValue("@file_path", imageItem.FilePath ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@file_name", imageItem.FileName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@is_animated", imageItem.IsAnimated);
                        command.Parameters.AddWithValue("@series_tag", imageItem.SeriesTag ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@base_chinese_name", imageItem.BaseChineseName ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@variant_number", imageItem.VariantNumber ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@has_variant", imageItem.HasVariant);
                        command.Parameters.AddWithValue("@thumbnail_data", thumbnailData ?? (object)DBNull.Value);
                        command.Parameters.AddWithValue("@last_modified", File.GetLastWriteTime(imageItem.FilePath ?? "").ToString("o"));
                        command.Parameters.AddWithValue("@file_hash", imageItem.FileHash ?? (object)DBNull.Value);
                        
                        command.ExecuteNonQuery();
                        
                        // 删除旧的标签
                        command.Parameters.Clear();
                        command.CommandText = "DELETE FROM image_tags WHERE image_id = (SELECT id FROM images WHERE file_path = @file_path)";
                        command.Parameters.AddWithValue("@file_path", imageItem.FilePath ?? (object)DBNull.Value);
                        command.ExecuteNonQuery();
                        
                        // 插入新的标签
                        if (imageItem.Tags != null)
                        {
                            foreach (var tag in imageItem.Tags)
                            {
                                command.Parameters.Clear();
                                command.CommandText = @"
                                    INSERT INTO image_tags (image_id, tag)
                                    VALUES ((SELECT id FROM images WHERE file_path = @file_path), @tag)";
                                command.Parameters.AddWithValue("@file_path", imageItem.FilePath ?? (object)DBNull.Value);
                                command.Parameters.AddWithValue("@tag", tag ?? (object)DBNull.Value);
                                command.ExecuteNonQuery();
                            }
                        }
                    }
                    
                    transaction.Commit();
                }
                catch (Exception ex)
                {
                    LoggingHelper.LogException(ex, "Error saving images to database");
                    throw;
                }
            });
        }
        
        public async Task<ImageItem?> LoadImageAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT id, file_path, file_name, is_animated, series_tag, base_chinese_name, variant_number, has_variant, thumbnail_data, last_modified, file_hash
                    FROM images 
                    WHERE file_path = @file_path";
                command.Parameters.AddWithValue("@file_path", filePath);
                
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var imageItem = new ImageItem
                    {
                        FilePath = reader.GetString(1),
                        FileName = reader.GetString(2),
                        IsAnimated = reader.GetBoolean(3),
                        SeriesTag = reader.IsDBNull(4) ? null : reader.GetString(4),
                        BaseChineseName = reader.IsDBNull(5) ? null : reader.GetString(5),
                        VariantNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                        HasVariant = reader.GetBoolean(7),
                        LastModified = reader.GetString(8),
                        FileHash = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Tags = new List<string>()
                    };
                    
                    // 加载缩略图数据
                    if (!reader.IsDBNull(8))
                    {
                        try
                        {
                            byte[] compressedData = (byte[])reader.GetValue(8);
                            byte[] thumbnailData = Decompress(compressedData);
                            imageItem.ThumbnailSource = ConvertBytesToBitmap(thumbnailData);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading thumbnail for {filePath}: {ex.Message}");
                        }
                    }
                    
                    // 加载标签
                    var tagCommand = connection.CreateCommand();
                    tagCommand.CommandText = "SELECT tag FROM image_tags WHERE image_id = @image_id";
                    tagCommand.Parameters.AddWithValue("@image_id", reader.GetInt32(0));
                    
                    using var tagReader = tagCommand.ExecuteReader();
                    while (tagReader.Read())
                    {
                        imageItem.Tags.Add(tagReader.GetString(0));
                    }
                    
                    return imageItem;
                }
                
                return null;
            });
        }
        
        public async Task<List<ImageItem>> LoadAllImagesAsync()
        {
            // 添加超时机制，防止数据库加载卡死
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1)); // 1分钟超时
            try
            {
                return await Task.Run(async () =>
                {
                    var images = new List<ImageItem>();
                    using var connection = new SqliteConnection($"Data Source={_databasePath}");
                    await connection.OpenAsync(cts.Token);
                    
                    var command = connection.CreateCommand();
                    command.CommandText = @"
                        SELECT id, file_path, file_name, is_animated, series_tag, base_chinese_name, variant_number, has_variant, last_modified
                        FROM images 
                        ORDER BY file_path";
                    
                    using var reader = await command.ExecuteReaderAsync(cts.Token);
                    while (await reader.ReadAsync(cts.Token))
                    {
                        var imageItem = new ImageItem
                        {
                            FilePath = reader.GetString(1),
                            FileName = reader.GetString(2),
                            IsAnimated = reader.GetBoolean(3),
                            SeriesTag = reader.IsDBNull(4) ? null : reader.GetString(4),
                            BaseChineseName = reader.IsDBNull(5) ? null : reader.GetString(5),
                            VariantNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                            HasVariant = reader.GetBoolean(7),
                            LastModified = reader.GetString(8),
                            Tags = new List<string>()
                        };
                        
                        images.Add(imageItem);
                    }
                    
                    // 加载所有图片的标签
                    foreach (var image in images)
                    {
                        var tagCommand = connection.CreateCommand();
                        tagCommand.CommandText = @"
                            SELECT tag FROM image_tags 
                            WHERE image_id = (SELECT id FROM images WHERE file_path = @file_path)";
                        tagCommand.Parameters.AddWithValue("@file_path", image.FilePath);
                        
                        using var tagReader = await tagCommand.ExecuteReaderAsync(cts.Token);
                        while (await tagReader.ReadAsync(cts.Token))
                        {
                            image.Tags.Add(tagReader.GetString(0));
                        }
                    }
                    
                    return images;
                }, cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Database loading timed out, using fresh load...");
                return new List<ImageItem>(); // 返回空列表，强制重新加载
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading images from database: {ex.Message}");
                return new List<ImageItem>(); // 返回空列表，强制重新加载
            }
        }
        
        public async Task<bool> NeedsUpdateAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                var lastWriteTime = File.GetLastWriteTime(filePath);
                
                using var connection = new SqliteConnection($"Data Source={_databasePath}");
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = "SELECT last_modified FROM images WHERE file_path = @file_path";
                command.Parameters.AddWithValue("@file_path", filePath);
                
                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var dbLastModified = DateTime.Parse(reader.GetString(0));
                    return lastWriteTime > dbLastModified;
                }
                
                // 如果数据库中没有记录，则需要更新
                return true;
            });
        }
        
        public async Task<BitmapImage?> LoadThumbnailAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var connection = new SqliteConnection($"Data Source={_databasePath}");
                    connection.Open();
                    
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT thumbnail_data FROM images WHERE file_path = @file_path";
                    command.Parameters.AddWithValue("@file_path", filePath);
                    
                    using var reader = command.ExecuteReader();
                    if (reader.Read() && !reader.IsDBNull(0))
                    {
                        try
                        {
                            byte[] compressedData = (byte[])reader.GetValue(0);
                            byte[] thumbnailData = Decompress(compressedData);
                            return ConvertBytesToBitmap(thumbnailData);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error loading thumbnail for {filePath}: {ex.Message}");
                        }
                    }
                    
                    return null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading thumbnail from database: {ex.Message}");
                    return null;
                }
            });
        }
        
        private byte[]? ConvertBitmapToBytes(BitmapImage bitmap)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(memoryStream);
                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting bitmap to bytes: {ex.Message}");
                return null;
            }
        }
        
        private BitmapImage ConvertBytesToBitmap(byte[] data)
        {
            using var memoryStream = new MemoryStream(data);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = memoryStream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        
        private byte[] Compress(byte[] data)
        {
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(output, CompressionMode.Compress))
            {
                gzip.Write(data, 0, data.Length);
            }
            return output.ToArray();
        }
        
        private byte[] Decompress(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            {
                gzip.CopyTo(output);
            }
            return output.ToArray();
        }
    }
}