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
using Microsoft.AspNetCore.WebUtilities;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

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
                // Determine Content-Type to handle both JSON and multipart/form-data
                if (!req.Headers.TryGetValues("Content-Type", out var contentTypeValues))
                {
                    throw new ArgumentException("Content-Type header is missing.");
                }

                var contentType = contentTypeValues.First();

                // Handle multipart/form-data
                if (!contentType.Contains("multipart/form-data"))
                {
                    throw new ArgumentException("Content-Type header is not multipart/form-data.");
                }

                // Extract boundary from Content-Type
                if (!contentType.Contains("boundary="))
                {
                    throw new ArgumentException("Content-Type header does not contain a boundary.");
                }

                var boundary = contentType.Split("boundary=").Last();
                var multipartReader = new MultipartReader(boundary, req.Body);
                MultipartSection section;

                byte[]? fileBytes = null;
                string? fileName = null;
                string? extension = null;
                string? blobContainer = null;
                string? blobConnectionString = null;
                string? subDirectory = null;
                bool useHashForFileName = false;
                bool deDupe = false;
                int originalWidth = 0, originalHeight = 0, sizedWidth = 0, sizedHeight = 0, thumbnailWidth = 0, thumbnailHeight = 0;

                while ((section = await multipartReader.ReadNextSectionAsync()) != null)
                {
                    //_logger.LogInformation($"Processing section with Content-Disposition: {section.ContentDisposition}");

                    if (section.ContentDisposition == null)
                    {
                        _logger.LogWarning("Section Content-Disposition is null. Skipping section.");
                        continue;
                    }

                    var contentDispositionHeader = System.Net.Http.Headers.ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                    if (contentDispositionHeader.DispositionType != "form-data")
                    {
                        _logger.LogWarning("Unexpected Content-Disposition type. Skipping section.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(contentDispositionHeader.Name))
                    {
                        _logger.LogWarning("Section Content-Disposition name is empty. Skipping section.");
                        continue;
                    }

                    // Normalize the name for case-insensitive comparison
                    var fieldName = contentDispositionHeader.Name.Trim('"').ToLower();

                    switch (fieldName)
                    {
                        case "file":
                            _logger.LogInformation("Reading file bytes...");
                            using (var ms = new MemoryStream())
                            {
                                await section.Body.CopyToAsync(ms);
                                fileBytes = ms.ToArray();
                                _logger.LogInformation($"File bytes length: {fileBytes.Length}");
                            }
                            break;

                        case "filename":
                            using (var reader = new StreamReader(section.Body))
                            {
                                fileName = await reader.ReadToEndAsync();
                                _logger.LogInformation($"File name: {fileName}");
                            }
                            break;

                        case "extension":
                            using (var reader = new StreamReader(section.Body))
                            {
                                extension = await reader.ReadToEndAsync();
                                //_logger.LogInformation($"Extension: {extension}");
                            }
                            break;

                        case "subdirectory":
                            using (var reader = new StreamReader(section.Body))
                            {
                                subDirectory = await reader.ReadToEndAsync();
                                //_logger.LogInformation($"Subdirectory: {subDirectory}");
                            }
                            break;

                        case "usehashforfilename":
                            using (var reader = new StreamReader(section.Body))
                            {
                                useHashForFileName = bool.Parse(await reader.ReadToEndAsync());
                                //_logger.LogInformation($"Use hash for file name: {useHashForFileName}");
                            }
                            break;

                        case "dedupe":
                            using (var reader = new StreamReader(section.Body))
                            {
                                deDupe = bool.Parse(await reader.ReadToEndAsync());
                                // _logger.LogInformation($"De-dupe: {deDupe}");
                            }
                            break;

                        case "blobcontainer":
                            using (var reader = new StreamReader(section.Body))
                            {
                                blobContainer = await reader.ReadToEndAsync();
                                // _logger.LogInformation($"Blob container: {blobContainer}");
                            }
                            break;

                        case "blobconnectionstring":
                            using (var reader = new StreamReader(section.Body))
                            {
                                blobConnectionString = await reader.ReadToEndAsync();
                                // _logger.LogInformation($"Blob connection string: {blobConnectionString}");
                            }
                            break;

                        case "originalwidth":
                            using (var reader = new StreamReader(section.Body))
                            {
                                originalWidth = int.Parse(await reader.ReadToEndAsync());
                                //_logger.LogInformation($"Original width: {originalWidth}");
                            }
                            break;

                        case "originalheight":
                            using (var reader = new StreamReader(section.Body))
                            {
                                originalHeight = int.Parse(await reader.ReadToEndAsync());
                                // _logger.LogInformation($"Original height: {originalHeight}");
                            }
                            break;

                        case "sizedwidth":
                            using (var reader = new StreamReader(section.Body))
                            {
                                sizedWidth = int.Parse(await reader.ReadToEndAsync());
                                //_logger.LogInformation($"Sized width: {sizedWidth}");
                            }
                            break;

                        case "sizedheight":
                            using (var reader = new StreamReader(section.Body))
                            {
                                sizedHeight = int.Parse(await reader.ReadToEndAsync());
                                //_logger.LogInformation($"Sized height: {sizedHeight}");
                            }
                            break;

                        case "thumbnailwidth":
                            using (var reader = new StreamReader(section.Body))
                            {
                                thumbnailWidth = int.Parse(await reader.ReadToEndAsync());
                                // _logger.LogInformation($"Thumbnail width: {thumbnailWidth}");
                            }
                            break;

                        case "thumbnailheight":
                            using (var reader = new StreamReader(section.Body))
                            {
                                thumbnailHeight = int.Parse(await reader.ReadToEndAsync());
                                // _logger.LogInformation($"Thumbnail height: {thumbnailHeight}");
                            }
                            break;

                        default:
                            _logger.LogWarning($"Unknown form-data field: {contentDispositionHeader.Name}");
                            break;
                    }
                }

                // Validate required fields
                if (fileBytes == null)
                {
                    _logger.LogError("ProcessImage: Missing file bytes in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'file' field is required and missing.");
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    _logger.LogError("ProcessImage: Missing fileName in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'fileName' field is required and missing.");
                }

                if (string.IsNullOrEmpty(extension))
                {
                    _logger.LogError("ProcessImage: Missing extension in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'extension' field is required and missing.");
                }

                if (string.IsNullOrEmpty(subDirectory))
                {
                    _logger.LogError("ProcessImage: Missing subDirectory in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'subDirectory' field is required and missing.");
                }

                if (string.IsNullOrEmpty(blobContainer))
                {
                    _logger.LogError("ProcessImage: Missing blobContainer in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'blobContainer' field is required and missing.");
                }

                if (string.IsNullOrEmpty(blobConnectionString))
                {
                    _logger.LogError("ProcessImage: Missing blobConnectionString in form-data.");
                    throw new ArgumentException("Invalid input parameters: 'blobConnectionString' field is required and missing.");
                }

                // Process the image
                return await ProcessImageAsync(req, deDupe, useHashForFileName, fileBytes, fileName, extension, subDirectory, blobContainer, blobConnectionString, originalWidth, originalHeight, sizedWidth, sizedHeight, thumbnailWidth, thumbnailHeight);
            }
            catch (Exception ex)
            {
                var errorDetails = new
                {
                    message = "Error processing image.",
                    exception = ex.Message,
                    stackTrace = ex.StackTrace
                };

                _logger.LogError(ex, "ProcessImage: Error processing image: {@errorDetails}", errorDetails);

                var errResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                errResponse.Headers.Add("Content-Type", "application/json");
                await errResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));

                return errResponse;
            }
        }

        private async Task<HttpResponseData> ProcessImageAsync(HttpRequestData req, bool deDupe, bool useHashForFileName, byte[] fileBytes, string fileName, string extension, string subDirectory, string blobContainer, string blobConnectionString, int originalWidth, int originalHeight, int sizedWidth, int sizedHeight, int thumbnailWidth, int thumbnailHeight)
        {
            var response = req.CreateResponse(System.Net.HttpStatusCode.OK);

            using var imageStream = new MemoryStream(fileBytes);
            using var image = Image.Load(imageStream);

            var blobServiceClient = new BlobServiceClient(blobConnectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(blobContainer);
            await containerClient.CreateIfNotExistsAsync();

            var imageService = new ImageProcessingService(_logger);

            // Generate MD5 hash of fileBytes, convert hash to string, use first 24 characters as file name
            if (useHashForFileName)
            {
                var hash = MD5.Create().ComputeHash(fileBytes);
                fileName = BitConverter.ToString(hash).Replace("-", "").ToLower().Substring(0, 24);
            }

            // Generate filenames
            var originalBlobName = $"{subDirectory}{fileName}_original.{extension}";
            var sizedBlobName = $"{subDirectory}{fileName}_sized.{extension}";
            var thumbnailBlobName = $"{subDirectory}{fileName}_thumbnail.{extension}";

            // De-dupe images by checking _original blob name
            if (deDupe)
            {
                _logger.LogInformation("ProcessImageAsync: Checking for dedupe: {originalBlobName}", originalBlobName);
                var blob = containerClient.GetBlobClient(originalBlobName);
                if (await blob.ExistsAsync())
                {
                    _logger.LogInformation("ProcessImageAsync: {originalBlobName} already exists. Skipping processing.", originalBlobName);
                    var existingResponseBody = new
                    {
                        original = blob.Uri.ToString(),
                        sized = "",
                        thumbnail = "",
                        message = "Image already exists. Skipping processing."
                    };

                    response.Headers.Add("Content-Type", "application/json");
                    await response.WriteStringAsync(JsonSerializer.Serialize(existingResponseBody));
                    return response;
                }
            }


            // Process images
            var originalImage = imageService.ResizeImage(image, originalWidth, originalHeight);
            var originalBlob = await imageService.UploadToBlobAsync(containerClient, originalImage, originalBlobName);

            var sizedImage = imageService.ResizeImage(image, sizedWidth, sizedHeight);
            var sizedBlob = await imageService.UploadToBlobAsync(containerClient, sizedImage, sizedBlobName);

            var thumbnailImage = imageService.ResizeImage(image, thumbnailWidth, thumbnailHeight);
            var thumbnailBlob = await imageService.UploadToBlobAsync(containerClient, thumbnailImage, thumbnailBlobName);

            _logger.LogInformation("ProcessImageAsync: Images processed and uploaded successfully.");

            var responseBody = new
            {
                original = originalBlob,
                sized = sizedBlob,
                thumbnail = thumbnailBlob
            };

            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));
            return response;
        }
    }
}
