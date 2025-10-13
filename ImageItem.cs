using System;
using System.Windows.Media.Imaging;

namespace PigPicPot
{
    public class ImageItem
    {
        public required string FilePath { get; set; }
        public required Uri FileUri { get; set; }
        public required string FullFileName { get; set; }
        public required string DisplayFileName { get; set; }
        public bool IsAnimated { get; set; }
        public BitmapSource? StartFrame { get; set; }
        public BitmapSource? MiddleFrame { get; set; }
        public BitmapSource? EndFrame { get; set; }
        public string BaseChineseName { get; set; } = string.Empty;
        public string BaseEnglishName { get; set; } = string.Empty;
        public bool IsSingleton { get; set; } = false;
        public string FullEnglishName { get; set; } = string.Empty;
        public string FullChineseName { get; set; } = string.Empty;
    }
}
