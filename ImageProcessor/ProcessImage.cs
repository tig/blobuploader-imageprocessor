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
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var input = JsonSerializer.Deserialize<ImageProcessingRequest>(requestBody);

                _logger.LogInformation($"ProcessImage: {JsonSerializer.Serialize(input.FileName)}.{JsonSerializer.Serialize(input.Extension)}");

                if (input == null || string.IsNullOrEmpty(input.ImageBase64) || string.IsNullOrEmpty(input.BlobContainer))
                {
                    throw new System.ArgumentException("Invalid input parameters.");
                }

                var imageBytes = System.Convert.FromBase64String(input.ImageBase64);

                // Generate a hash of the image and use it as the filename
                var hash = System.Security.Cryptography.MD5.Create();
                var hashBytes = hash.ComputeHash(imageBytes);
                // Convert to hex string and use just the last 24 characters
                var hashString = System.BitConverter.ToString(hashBytes).Replace("-", "").ToLower().Substring(8, 24);

                if (input.UseHashForFileName)
                {
                    input.FileName = hashString;
                }

                var blobServiceClient = new BlobServiceClient(input.BlobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(input.BlobContainer);
                await containerClient.CreateIfNotExistsAsync();

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