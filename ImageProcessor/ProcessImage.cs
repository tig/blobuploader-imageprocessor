using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ImageProcessor.Models;
using ImageProcessor.Services;
using SixLabors.ImageSharp;

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
            try
            {
                // Deserialize the request
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation("Request body read successfully.");

                var input = JsonSerializer.Deserialize<ImageProcessingRequest>(requestBody);
                _logger.LogInformation("Deserialized request payload: {@input}", input);

                if (input == null)
                {
                    _logger.LogError("Input payload is null.");
                    throw new ArgumentException("Input payload cannot be null.");
                }

                if (string.IsNullOrEmpty(input.ImageBase64) || string.IsNullOrEmpty(input.BlobContainer))
                {
                    _logger.LogError("Invalid input parameters: ImageBase64 or BlobContainer is null or empty.");
                    throw new ArgumentException("Invalid input parameters.");
                }

                // Validate Base64 string
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(input.ImageBase64);
                    _logger.LogInformation("ImageBase64 decoded successfully.");
                }
                catch (FormatException ex)
                {
                    _logger.LogError(ex, "Invalid Base64 string.");
                    throw new ArgumentException("Invalid Base64 string.", ex);
                }

                // Create BlobServiceClient
                _logger.LogInformation("Creating BlobServiceClient.");
                var blobServiceClient = new BlobServiceClient(input.BlobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(input.BlobContainer);
                await containerClient.CreateIfNotExistsAsync();
                
                _logger.LogInformation("Blob container ensured.");
                var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

                var originalBlobName = input.UploadPath + input.FileName + "_original." + input.Extension;
                var sizedBlobName = input.UploadPath + input.FileName + "_sized." + input.Extension;
                var thumbnailBlobName = input.UploadPath + input.FileName + "_thumbnail." + input.Extension;

                // Check for duplicates in UploadPath
                if (input.DeDupe)
                {
                    var blobClient = containerClient.GetBlobClient(originalBlobName);
                    if (await blobClient.ExistsAsync())
                    {
                        _logger.LogInformation("File already exists: {originalBlobName}", originalBlobName);
                        response.Headers.Add("Content-Type", "application/json");
                        var responseBody = new { original = blobClient.Uri.ToString(), sized = blobClient.Uri.ToString(), thumbnail = blobClient.Uri.ToString() };
                        await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));

                        return response;
                    }
                }

                _logger.LogInformation("File doesn't exist: {originalBlobName}", originalBlobName);

                using var imageStream = new MemoryStream(imageBytes);
                using var image = Image.Load(imageStream);

                var imageService = new ImageProcessingService();

                var thumbnailImage = imageService.ResizeImage(image, input.ThumbnailWidth, input.ThumbnailHeight);
                var thumbnailBlob = await imageService.UploadToBlobAsync(containerClient, thumbnailImage, thumbnailBlobName);

                var sizedImage = imageService.ResizeImage(image, input.SizedWidth, input.SizedHeight);
                var sizedBlob = await imageService.UploadToBlobAsync(containerClient, sizedImage, sizedBlobName);

                var originalImage = imageService.ResizeImage(image, input.OriginalWidth, input.OriginalHeight);
                var originalBlob = await imageService.UploadToBlobAsync(containerClient, originalImage, originalBlobName);

                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new { original = originalBlob, sized = sizedBlob, thumbnail = thumbnailBlob }));

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image.");
                var errResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errResponse.WriteStringAsync("Error processing image.");
                return errResponse;
            }
        }
    }
}