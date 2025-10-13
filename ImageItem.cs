using System.Collections.Generic;
using System.ComponentModel;

public class ImageItem : INotifyPropertyChanged
{
    private string _filePath = string.Empty;
    private string _fileName = string.Empty;
    private bool _isAnimated;
    private List<string> _tags = new List<string>();
    private string? _seriesTag;
    private string? _baseChineseName;
    private string? _variantNumber;
    private bool _hasVariant;
    private System.Windows.Media.Imaging.BitmapSource? _thumbnailSource;
    private volatile bool _isThumbnailQueued;
    private bool _isCorrupted;

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }
    
    public string FileName
    {
        get => _fileName;
        set
        {
            _fileName = value;
            OnPropertyChanged(nameof(FileName));
        }
    }
    
    public bool IsAnimated
    {
        get => _isAnimated;
        set
        {
            _isAnimated = value;
            OnPropertyChanged(nameof(IsAnimated));
        }
    }
    
    public List<string> Tags
    {
        get => _tags;
        set
        {
            _tags = value;
            OnPropertyChanged(nameof(Tags));
        }
    }
    
    public string? SeriesTag
    {
        get => _seriesTag;
        set
        {
            _seriesTag = value;
            OnPropertyChanged(nameof(SeriesTag));
        }
    }

    public string? BaseChineseName
    {
        get => _baseChineseName;
        set
        {
            _baseChineseName = value;
            OnPropertyChanged(nameof(BaseChineseName));
        }
    }

    public string? VariantNumber
    {
        get => _variantNumber;
        set
        {
            _variantNumber = value;
            OnPropertyChanged(nameof(VariantNumber));
        }
    }

    public bool HasVariant
    {
        get => _hasVariant;
        set
        {
            _hasVariant = value;
            OnPropertyChanged(nameof(HasVariant));
        }
    }
    
    public System.Windows.Media.Imaging.BitmapSource? ThumbnailSource
    {
        get => _thumbnailSource;
        set
        {
            _thumbnailSource = value;
            OnPropertyChanged(nameof(ThumbnailSource));
        }
    }
    
    public bool IsThumbnailQueued
    {
        get => _isThumbnailQueued;
        set
        {
            _isThumbnailQueued = value;
            OnPropertyChanged(nameof(IsThumbnailQueued));
        }
    }

    public bool IsCorrupted
    {
        get => _isCorrupted;
        set
        {
            _isCorrupted = value;
            OnPropertyChanged(nameof(IsCorrupted));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}