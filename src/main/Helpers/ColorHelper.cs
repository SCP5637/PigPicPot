using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PigPicPot.Helpers
{
    public static class ColorHelper
    {
        /// <summary>
        /// Determines if an image is predominantly light or dark.
        /// </summary>
        /// <param name="imagePath">The file path of the image to analyze.</param>
        /// <returns>True if the image is considered light, false if it's dark.</returns>
        public static bool IsImageLight(string imagePath)
        {
            try
            {
                var bitmap = new BitmapImage(new Uri(imagePath));
                var frame = BitmapFrame.Create(bitmap);

                // Resize for performance. We don't need to check every pixel.
                var resized = new TransformedBitmap(frame, new ScaleTransform(0.1, 0.1));
                var format = PixelFormats.Bgra32;
                var stride = resized.PixelWidth * (format.BitsPerPixel / 8);
                var pixels = new byte[resized.PixelHeight * stride];
                resized.CopyPixels(pixels, stride, 0);

                long totalBrightness = 0;
                int pixelCount = resized.PixelWidth * resized.PixelHeight;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    // BGRA format
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    
                    // Simple brightness calculation (Luma)
                    double brightness = (0.299 * r + 0.587 * g + 0.114 * b);
                    totalBrightness += (long)brightness;
                }

                double avgBrightness = (double)totalBrightness / pixelCount;

                // Threshold can be adjusted. 128 is the midpoint.
                return avgBrightness > 128;
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException(ex, "Failed to analyze image color.");
                // Default to dark text on light background assumption
                return true; 
            }
        }

        /// <summary>
        /// Gets a high-contrast foreground color based on the background image's brightness.
        /// </summary>
        /// <param name="imagePath">The file path of the background image.</param>
        /// <returns>A SolidColorBrush (either Black or White).</returns>
        public static SolidColorBrush GetHighContrastForegroundColor(string imagePath)
        {
            return IsImageLight(imagePath) ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
        }
    }
}