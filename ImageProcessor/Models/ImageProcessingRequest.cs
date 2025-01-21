namespace ImageProcessor.Models;

public class ImageProcessingRequest
{
    public string ImageBase64 { get; set; }
    public string UploadPath {get;set;}
    public bool UseHashForFileName {get;set;}
    public bool DeDupe {get;set;}
    public string FileName { get; set; }
    public string Extension { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int SizedWidth { get; set; }
    public int SizedHeight { get; set; }
    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    public string BlobConnectionString { get; set; }
    public string BlobContainer { get; set; }
}