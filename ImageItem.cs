namespace PigPicPot
{
    public class ImageItem
    {
        public required string FilePath { get; set; }
        public required string FullFileName { get; set; }
        public required string DisplayFileName { get; set; }
        public bool IsGif { get; set; }
        public required Uri FileUri { get; set; }
    }
}
