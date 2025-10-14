using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using System.IO;
using System.Threading.Tasks;

namespace PigPicPot.Services
{
    public class ImageProcessingService
    {
        public async Task<bool> RepairGifAsync(string filePath)
        {
            string tempFilePath = Path.GetTempFileName();
            try
            {
                // Load the image with ImageSharp. This will perform a full decode.
                using (var image = await SixLabors.ImageSharp.Image.LoadAsync(filePath))
                {
                    // If it's not an animated GIF, no repair is needed.
                    if (image.Frames.Count <= 1)
                    {
                        return true;
                    }

                    // Save the image to a temporary path. ImageSharp will re-encode it correctly.
                    await image.SaveAsync(tempFilePath, new GifEncoder());
                }

                // Overwrite the original file with the repaired one.
                File.Move(tempFilePath, filePath, true);
                return true;
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[ImageProcessingService] Failed to repair GIF {filePath}: {ex.Message}");
                // Clean up the temp file if it exists
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
                return false;
            }
        }
    }
}
