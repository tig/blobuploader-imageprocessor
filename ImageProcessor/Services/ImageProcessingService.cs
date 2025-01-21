using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.IO;
using System.Threading.Tasks;

namespace ImageProcessor.Services
{
    public class ImageProcessingService
    {
        public Image ResizeImage(Image image, int width, int height)
        {
            return image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));
        }

        public async Task<string> UploadToBlobAsync(
            BlobContainerClient containerClient, 
            Image image, 
            string fileName)
        {
            using var ms = new MemoryStream();
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