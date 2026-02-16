using System;
using System.Windows.Media.Imaging;

namespace PigPicPot
{
    public class ImageItem
    {
        public byte[]? Data { get; set; }
        public string? TempPath { get; set; }
        public required BitmapSource Thumbnail { get; set; }
        public bool IsAnimated { get; set; }
    }
}
