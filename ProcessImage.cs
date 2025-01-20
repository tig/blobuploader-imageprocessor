using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Azure.Functions.Worker.Http;

namespace ImageProcessor
{
    public class ProcessImage
    {
        private readonly ILogger<ProcessImage> _logger;

        public ProcessImage(ILogger<ProcessImage> logger)
        {
            _logger = logger;
        }

        [Function("ProcessImage")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("Processing image...");

            try
            {
                // Parse input parameters
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var input = JsonSerializer.Deserialize<ImageProcessingRequest>(requestBody);

                // Validate input
                if (input == null || string.IsNullOrEmpty(input.ImageBase64) || string.IsNullOrEmpty(input.BlobContainer))
                {
                    throw new ArgumentException("Invalid input parameters.");
                }

                // Decode the base64 image
                var imageBytes = Convert.FromBase64String(input.ImageBase64);
                using var imageStream = new MemoryStream(imageBytes);
                using var image = Image.Load(imageStream);

                // Process images
                var originalImage = ResizeImage(image, input.OriginalWidth, input.OriginalHeight);
                var sizedImage = ResizeImage(image, input.SizedWidth, input.SizedHeight);
                var thumbnailImage = ResizeImage(image, input.ThumbnailWidth, input.ThumbnailHeight);

                // Save images to Azure Blob Storage
                var blobServiceClient = new BlobServiceClient(input.BlobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(input.BlobContainer);
                await containerClient.CreateIfNotExistsAsync();

                var originalBlob = await UploadToBlobAsync(containerClient, originalImage, input.FileName, "original", input.Extension);
                var sizedBlob = await UploadToBlobAsync(containerClient, sizedImage, input.FileName, "sized", input.Extension);
                var thumbnailBlob = await UploadToBlobAsync(containerClient, thumbnailImage, input.FileName, "thumbnail", input.Extension);

                // Prepare response
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                var responseBody = new
                {
                    original = originalBlob,
                    sized = sizedBlob,
                    thumbnail = thumbnailBlob
                };
                await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));

                _logger.LogInformation("Image processing completed successfully.");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private static Image ResizeImage(Image image, int width, int height)
        {
            return image.Clone(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Max
            }));
        }

        private static async Task<string> UploadToBlobAsync(BlobContainerClient containerClient, Image image, string fileName, string suffix, string extension)
        {
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 85 });
            ms.Position = 0;

            var blobName = $"{Path.GetFileNameWithoutExtension(fileName)}_{suffix}.{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(ms, overwrite: true);

            return blobClient.Uri.ToString();
        }

        private class ImageProcessingRequest
        {
            public string ImageBase64 { get; set; }
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
    }
}
