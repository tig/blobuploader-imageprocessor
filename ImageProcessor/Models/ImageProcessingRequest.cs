namespace ImageProcessor.Models;

public class ImageProcessingRequest
{
    public required string ImageBase64 { get; set; }
    public bool UseHashForFileName {get;set;}
    public bool DeDupe {get;set;}
    public required string FileName { get; set; }
    public required string Extension { get; set; }
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int SizedWidth { get; set; }
    public int SizedHeight { get; set; }
    public int ThumbnailWidth { get; set; }
    public int ThumbnailHeight { get; set; }
    public required string BlobConnectionString { get; set; }
    public required string BlobContainer { get; set; }    
    public required string SubDirectory {get;set;}
}