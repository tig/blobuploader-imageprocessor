using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Azure.Storage.Blobs;
using System.IO;
using System.Threading.Tasks;

namespace ImageProcessor
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
            string fileName, 
            string suffix, 
            string extension)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 85 });
            ms.Position = 0;

            var blobName = $"{Path.GetFileNameWithoutExtension(fileName)}_{suffix}.{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(ms, overwrite: true);

            return blobClient.Uri.ToString();
        }
    }
}
