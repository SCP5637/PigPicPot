using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using System.IO;
using System.Threading.Tasks;

namespace PigPicPot.Services
{
    /// <summary>
    /// 图像处理服务，提供图像处理功能
    /// Image processing service, provides image processing functionality
    /// </summary>
    public class ImageProcessingService
    {
        /// <summary>
        /// 异步修复GIF文件
        /// Asynchronously repair GIF file
        /// </summary>
        /// <param name="filePath">GIF文件路径</param>
        /// <returns>修复是否成功</returns>
        public async Task<bool> RepairGifAsync(string filePath)
        {
            // 修复：添加文件访问冲突处理
            for (int i = 0; i < 3; i++) // 重试3次
            {
                string tempFilePath = Path.GetTempFileName();
                try
                {
                    using (var image = await SixLabors.ImageSharp.Image.LoadAsync(filePath))
                    {
                        // 如果只有一帧，不需要修复
                        // If only one frame, no need to repair
                        if (image.Frames.Count <= 1)
                        {
                            return true;
                        }

                        await image.SaveAsync(tempFilePath, new GifEncoder());
                    }

                    File.Move(tempFilePath, filePath, true);
                    return true;
                }
                catch (System.IO.IOException ioEx)
                {
                    System.Console.WriteLine($"[ImageProcessingService] IO Exception when repairing GIF {filePath} (attempt {i + 1}): {ioEx.Message}");
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                    
                    // 等待一段时间再重试
                    await Task.Delay(100 * (i + 1));
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[ImageProcessingService] Failed to repair GIF {filePath}: {ex.Message}");
                    if (File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                    return false;
                }
            }
            
            return false;
        }
    }
}