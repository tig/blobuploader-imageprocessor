using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcessor.Services
{
    public class ImageProcessingService
    {
         private readonly ILogger _logger;

        public ImageProcessingService(ILogger logger)
        {
            _logger = logger;
        }
        
        public Image ResizeImage(Image image, int width, int height)
        {
            _logger.LogInformation($"Resizing image to {width}x{height}");
            return image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));
        }

        public Image ProcessAnimatedGif(Image image, int width, int height)
        {
            var gifEncoder = new GifEncoder();
            var resizedGif = new Image<Rgba32>(width, height);

            foreach (var frame in image.Frames)
            {
                var resizedFrame = ResizeImage(frame.Clone(), width, height);
                resizedGif.Frames.AddFrame(resizedFrame.Frames.RootFrame);
            }

            using var ms = new MemoryStream();
            resizedGif.Save(ms, gifEncoder);
            ms.Position = 0;

            return Image.Load(ms);
        }


        public async Task<string> UploadToBlobAsync(
            BlobContainerClient containerClient, 
            Image image, 
            string fileName)
        {
            _logger.LogInformation($"Uploading image to blob storage: {fileName}");
            using var ms = new MemoryStream();

            // TODO: Move quality to be configurable param
            image.Save(ms, new JpegEncoder { Quality = 85 });
            ms.Position = 0;

            var blobClient = containerClient.GetBlobClient(fileName);

            var contentType = GetContentType(fileName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(ms, new BlobUploadOptions
            {
                HttpHeaders = blobHttpHeaders,
            });

            return blobClient.Uri.ToString();
        }

        
        private string GetContentType(string path)
        {
            var extension = Path.GetExtension(path).TrimStart('.');
            return extension.ToLower() switch
            {
                "jpg" => "image/jpeg",
                "jpeg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "bmp" => "image/bmp",
                "tiff" => "image/tiff",
                "webp" => "image/webp",
                "svg" => "image/svg+xml",
                "ico" => "image/x-icon",
                "pdf" => "application/pdf",
                "heic" => "image/heic",
                _ => "application/octet-stream",
            };
        }
    }
}