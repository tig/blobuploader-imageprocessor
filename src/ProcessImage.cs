using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ImageProcessor
{
    public class ProcessImage
    {
        private readonly ILogger<ProcessImage> _logger;
        private readonly ImageProcessingService _imageService;

        public ProcessImage(ILogger<ProcessImage> logger, ImageProcessingService imageService)
        {
            _logger = logger;
            _imageService = imageService;
        }

        [Function("ProcessImage")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
        {
            _logger.LogInformation("Processing image...");

            try
            {
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var input = JsonSerializer.Deserialize<ImageProcessingRequest>(requestBody);

                if (input == null || string.IsNullOrEmpty(input.ImageBase64) || string.IsNullOrEmpty(input.BlobContainer))
                {
                    throw new System.ArgumentException("Invalid input parameters.");
                }

                var imageBytes = System.Convert.FromBase64String(input.ImageBase64);
                using var imageStream = new MemoryStream(imageBytes);
                using var image = Image.Load(imageStream);

                var originalImage = _imageService.ResizeImage(image, input.OriginalWidth, input.OriginalHeight);
                var sizedImage = _imageService.ResizeImage(image, input.SizedWidth, input.SizedHeight);
                var thumbnailImage = _imageService.ResizeImage(image, input.ThumbnailWidth, input.ThumbnailHeight);

                var blobServiceClient = new BlobServiceClient(input.BlobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(input.BlobContainer);
                await containerClient.CreateIfNotExistsAsync();

                var originalBlob = await _imageService.UploadToBlobAsync(containerClient, originalImage, input.FileName, "original", input.Extension);
                var sizedBlob = await _imageService.UploadToBlobAsync(containerClient, sizedImage, input.FileName, "sized", input.Extension);
                var thumbnailBlob = await _imageService.UploadToBlobAsync(containerClient, thumbnailImage, input.FileName, "thumbnail", input.Extension);

                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                var responseBody = new { original = originalBlob, sized = sizedBlob, thumbnail = thumbnailBlob };
                await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));

                _logger.LogInformation("Image processing completed successfully.");
                return response;
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error processing image.");
                var errorResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}