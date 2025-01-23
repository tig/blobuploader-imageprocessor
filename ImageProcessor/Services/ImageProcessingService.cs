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
            _logger.LogInformation($"PI: Resizing image to {width}x{height}");

            // Check if the specified size is larger than the original dimensions
            if (width >= image.Width && height >= image.Height)
            {
                _logger.LogInformation("PI: Specified size is larger than the original dimensions. Returning the original image.");
                return image;
            }

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            _logger.LogInformation("PI: Image resizing complete.");
            return image;
        }

        public Image ResizeAnimatedGif(Image image, int width, int height)
        {
            _logger.LogInformation($"PI: Resizing animated GIF to fit within {width}x{height}");

            // Check if the specified size is larger than the original dimensions
            if (width >= image.Width && height >= image.Height)
            {
                _logger.LogInformation("PI: Specified size is larger than the original dimensions. Returning the original image.");
                return image;
            }

            // Resize the entire GIF
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));

            _logger.LogInformation("PI: Animated GIF resizing complete.");
            return image;
        }

        public async Task<string> UploadToBlobAsync(
            BlobContainerClient containerClient,
            Image image,
            string fileName)
        {
            _logger.LogInformation($"PI: Uploading image to blob storage: {fileName}");
            using var ms = new MemoryStream();

            // Get the image format
            var format = image.Metadata.DecodedImageFormat;

            // Save the image using the appropriate encoder
            image.Save(ms, format);
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