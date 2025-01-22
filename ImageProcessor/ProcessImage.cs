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
                _logger.LogInformation("ProcessImage: requestBody: {requestBody}", req.Body);

                // Deserialize the request
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation("ProcessImage: Request body read successfully.");

                var input = JsonSerializer.Deserialize<ImageProcessingRequest>(requestBody);
                _logger.LogInformation("ProcessImage: Deserialized request payload: {@input}", JsonSerializer.Serialize(input));

                if (input is null)
                {
                    _logger.LogError("ProcessImage: Input payload is null.");
                    throw new ArgumentException("Input payload cannot be null.");
                }

                if (string.IsNullOrEmpty(input.BlobContainer)){
                    _logger.LogError("ProcessImage: BlobContainer empty.");
                    throw new ArgumentException("Invalid input parameters: BlobContainer is empty.");
                }

                if (string.IsNullOrEmpty(input.ImageBase64))
                {
                    _logger.LogError("ProcessImage: ImageBase64 empty.");
                    throw new ArgumentException("Invalid input parameters: ImageBase64 is empty.");
                }

                // Validate Base64 string
                byte[] imageBytes;
                try
                {
                    imageBytes = Convert.FromBase64String(input.ImageBase64);
                    _logger.LogInformation("ProcessImage: ImageBase64 decoded successfully.");
                }
                catch (FormatException ex)
                {
                    _logger.LogError(ex, "ProcessImage: Invalid Base64 string.");
                    throw new ArgumentException("Invalid Base64 string.", ex);
                }

                // Create BlobServiceClient
                _logger.LogInformation("ProcessImage: Creating BlobServiceClient.");
                var blobServiceClient = new BlobServiceClient(input.BlobConnectionString);
                var containerClient = blobServiceClient.GetBlobContainerClient(input.BlobContainer);
                await containerClient.CreateIfNotExistsAsync();

                _logger.LogInformation("ProcessImage: Blob container ensured.");
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
                        _logger.LogInformation("ProcessImage: File already exists: {originalBlobName}", originalBlobName);
                        response.Headers.Add("Content-Type", "application/json");
                        var responseBody = new { original = blobClient.Uri.ToString(), sized = blobClient.Uri.ToString(), thumbnail = blobClient.Uri.ToString() };
                        await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));

                        return response;
                    }
                }

                _logger.LogInformation("ProcessImage: File doesn't exist: {originalBlobName}", originalBlobName);

                using var imageStream = new MemoryStream(imageBytes);
                using var image = Image.Load(imageStream);

                var imageService = new ImageProcessingService();

                _logger.LogInformation("ProcessImage: Resizing thumbnail.");
                var thumbnailImage = imageService.ResizeImage(image, input.ThumbnailWidth, input.ThumbnailHeight);

                _logger.LogInformation("ProcessImage: Uploading thumbnail to Blob storage.");
                var thumbnailBlob = await imageService.UploadToBlobAsync(containerClient, thumbnailImage, thumbnailBlobName);

                _logger.LogInformation("ProcessImage: Resizing sized.");
                var sizedImage = imageService.ResizeImage(image, input.SizedWidth, input.SizedHeight);
                _logger.LogInformation("ProcessImage: Uploading sized to Blob storage.");
                var sizedBlob = await imageService.UploadToBlobAsync(containerClient, sizedImage, sizedBlobName);

                _logger.LogInformation("ProcessImage: Resizing original.");
                var originalImage = imageService.ResizeImage(image, input.OriginalWidth, input.OriginalHeight);
                _logger.LogInformation("ProcessImage: Uploading original to Blob storage.");
                var originalBlob = await imageService.UploadToBlobAsync(containerClient, originalImage, originalBlobName);

                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(new { original = originalBlob, sized = sizedBlob, thumbnail = thumbnailBlob }));

                _logger.LogInformation("ProcessImage: Image processing completed successfully.", response);
                return response;
            }
            catch (Exception ex)
            {
                var errorDetails = new
                {
                    message = "Error processing image.",
                    exception = ex.Message,
                    stackTrace = ex.StackTrace,
                    input = req.Body
                };

                _logger.LogError(ex, "ProcessImage: Error processing image: {@errorDetails}", errorDetails);

                var errResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                errResponse.Headers.Add("Content-Type", "application/json");
                await errResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));

                return errResponse;
            }

        }
    }
}