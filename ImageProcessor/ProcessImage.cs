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
using System.Diagnostics;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageProcessor;

public class ProcessImage
{
    private readonly ILogger<ProcessImage> _logger;
    private readonly Stack<Stopwatch> _stopwatches = new Stack<Stopwatch>();

    public ProcessImage(ILogger<ProcessImage> logger)
    {
        _logger = logger;
    }

    [Function("ProcessImage")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestData req, FunctionContext context)
    {
        StartPerfLog("======== ProcessImage");

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
                    _logger.LogWarning("PI: Section Content-Disposition is null. Skipping section.");
                    continue;
                }

                var contentDispositionHeader = System.Net.Http.Headers.ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                if (contentDispositionHeader.DispositionType != "form-data")
                {
                    _logger.LogWarning("PI: Unexpected Content-Disposition type. Skipping section.");
                    continue;
                }

                if (string.IsNullOrEmpty(contentDispositionHeader.Name))
                {
                    _logger.LogWarning("PI: Section Content-Disposition name is empty. Skipping section.");
                    continue;
                }

                // Normalize the name for case-insensitive comparison
                var fieldName = contentDispositionHeader.Name.Trim('"').ToLower();

                switch (fieldName)
                {
                    case "file":
                        _logger.LogInformation("PI: Reading file bytes...");
                        using (var ms = new MemoryStream())
                        {
                            await section.Body.CopyToAsync(ms);
                            fileBytes = ms.ToArray();
                            _logger.LogInformation($"PI: File bytes length: {fileBytes.Length}");
                        }
                        break;

                    case "filename":
                        using (var reader = new StreamReader(section.Body))
                        {
                            fileName = await reader.ReadToEndAsync();
                            _logger.LogInformation($"PI: File name: {fileName}");
                        }
                        break;

                    case "extension":
                        using (var reader = new StreamReader(section.Body))
                        {
                            extension = await reader.ReadToEndAsync();
                            //_logger.LogInformation($"PI: Extension: {extension}");
                        }
                        break;

                    case "subdirectory":
                        using (var reader = new StreamReader(section.Body))
                        {
                            subDirectory = await reader.ReadToEndAsync();
                            //_logger.LogInformation($"PI: Subdirectory: {subDirectory}");
                        }
                        break;

                    case "usehashforfilename":
                        using (var reader = new StreamReader(section.Body))
                        {
                            useHashForFileName = bool.Parse(await reader.ReadToEndAsync());
                            //_logger.LogInformation($"PI: Use hash for file name: {useHashForFileName}");
                        }
                        break;

                    case "dedupe":
                        using (var reader = new StreamReader(section.Body))
                        {
                            deDupe = bool.Parse(await reader.ReadToEndAsync());
                            // _logger.LogInformation($"PI: De-dupe: {deDupe}");
                        }
                        break;

                    case "blobcontainer":
                        using (var reader = new StreamReader(section.Body))
                        {
                            blobContainer = await reader.ReadToEndAsync();
                            // _logger.LogInformation($"PI: Blob container: {blobContainer}");
                        }
                        break;

                    case "blobconnectionstring":
                        using (var reader = new StreamReader(section.Body))
                        {
                            blobConnectionString = await reader.ReadToEndAsync();
                            // _logger.LogInformation($"PI: Blob connection string: {blobConnectionString}");
                        }
                        break;

                    case "originalwidth":
                        using (var reader = new StreamReader(section.Body))
                        {
                            originalWidth = int.Parse(await reader.ReadToEndAsync());
                            //_logger.LogInformation($"PI: Original width: {originalWidth}");
                        }
                        break;

                    case "originalheight":
                        using (var reader = new StreamReader(section.Body))
                        {
                            originalHeight = int.Parse(await reader.ReadToEndAsync());
                            // _logger.LogInformation($"PI: Original height: {originalHeight}");
                        }
                        break;

                    case "sizedwidth":
                        using (var reader = new StreamReader(section.Body))
                        {
                            sizedWidth = int.Parse(await reader.ReadToEndAsync());
                            //_logger.LogInformation($"PI: Sized width: {sizedWidth}");
                        }
                        break;

                    case "sizedheight":
                        using (var reader = new StreamReader(section.Body))
                        {
                            sizedHeight = int.Parse(await reader.ReadToEndAsync());
                            //_logger.LogInformation($"PI: Sized height: {sizedHeight}");
                        }
                        break;

                    case "thumbnailwidth":
                        using (var reader = new StreamReader(section.Body))
                        {
                            thumbnailWidth = int.Parse(await reader.ReadToEndAsync());
                            // _logger.LogInformation($"PI: Thumbnail width: {thumbnailWidth}");
                        }
                        break;

                    case "thumbnailheight":
                        using (var reader = new StreamReader(section.Body))
                        {
                            thumbnailHeight = int.Parse(await reader.ReadToEndAsync());
                            // _logger.LogInformation($"PI: Thumbnail height: {thumbnailHeight}");
                        }
                        break;

                    default:
                        _logger.LogWarning($"PI: Unknown form-data field: {contentDispositionHeader.Name}");
                        break;
                }
            }

            // Validate required fields
            if (fileBytes == null)
            {
                throw new ArgumentException("Invalid input parameters: 'file' field is required and missing.");
            }

            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentException("Invalid input parameters: 'fileName' field is required and missing.");
            }

            if (string.IsNullOrEmpty(extension))
            {
                throw new ArgumentException("Invalid input parameters: 'extension' field is required and missing.");
            }

            if (string.IsNullOrEmpty(subDirectory))
            {
                throw new ArgumentException("Invalid input parameters: 'subDirectory' field is required and missing.");
            }

            if (string.IsNullOrEmpty(blobContainer))
            {
                throw new ArgumentException("Invalid input parameters: 'blobContainer' field is required and missing.");
            }

            if (string.IsNullOrEmpty(blobConnectionString))
            {
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

            _logger.LogError(ex, "PI: Error processing image: {@errorDetails}", errorDetails);

            var errResponse = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            errResponse.Headers.Add("Content-Type", "application/json");
            await errResponse.WriteStringAsync(JsonSerializer.Serialize(errorDetails));

            return errResponse;
        }
        finally
        {
            StopPerfLog("======== ProcessImage");
        }
    }

    private async Task<HttpResponseData> ProcessImageAsync(HttpRequestData req, bool deDupe, bool useHashForFileName, byte[] fileBytes, string fileName, string extension, string subDirectory, string blobContainer, string blobConnectionString, int originalWidth, int originalHeight, int sizedWidth, int sizedHeight, int thumbnailWidth, int thumbnailHeight)
    {
        StartPerfLog("ProcessImageAsync");
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
                    original = containerClient.Uri + originalBlobName,
                    sized = containerClient.Uri + sizedBlobName,
                    thumbnail = containerClient.Uri + thumbnailBlobName,
                    message = "Image already exists. Skipping processing.",
                    processingTime = StopPerfLog("ProcessImageAsync"),
                };

                response.Headers.Add("Content-Type", "application/json");
                await response.WriteStringAsync(JsonSerializer.Serialize(existingResponseBody));

                return response;
            }
        }

        var originalBlob = string.Empty;
        var sizedBlob = string.Empty;
        var thumbnailBlob = string.Empty;

        if (image.Frames.Count > 1)
        {
            // Handle animated GIF
            var originalGif = imageService.ResizeAnimatedGif(image, originalWidth, originalHeight);
            originalBlob = await imageService.UploadToBlobAsync(containerClient, originalGif, originalBlobName);

            var sizedGif = imageService.ResizeAnimatedGif(image, sizedWidth, sizedHeight);
            sizedBlob = await imageService.UploadToBlobAsync(containerClient, sizedGif, sizedBlobName);

            var thumbnailGif = imageService.ResizeAnimatedGif(image, thumbnailWidth, thumbnailHeight);
            thumbnailBlob = await imageService.UploadToBlobAsync(containerClient, thumbnailGif, thumbnailBlobName);
        } 
        else
        {
            // Process images
            StartPerfLog("originalImage = imageService.ResizeImage");
            var originalImage = imageService.ResizeImage(image, originalWidth, originalHeight);
            StopPerfLog("originalImage = imageService.ResizeImage");

            originalBlob = await imageService.UploadToBlobAsync(containerClient, originalImage, originalBlobName);

            StartPerfLog("sizedImage = imageService.ResizeImage");
            var sizedImage = imageService.ResizeImage(image, sizedWidth, sizedHeight);
            StopPerfLog("sizedImage = imageService.ResizeImage");

            sizedBlob = await imageService.UploadToBlobAsync(containerClient, sizedImage, sizedBlobName);

            var thumbnailImage = imageService.ResizeImage(image, thumbnailWidth, thumbnailHeight);
            thumbnailBlob = await imageService.UploadToBlobAsync(containerClient, thumbnailImage, thumbnailBlobName);

        }

        _logger.LogInformation("PI: Images processed and uploaded successfully.");

        var responseBody = new
        {
            original = originalBlob,
            sized = sizedBlob,
            thumbnail = thumbnailBlob,
            processingTime = StopPerfLog("ProcessImageAsync"),
        };

        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(responseBody));
        return response;
    }

    private void StartPerfLog(string operationName)
    {
        _logger.LogInformation($"PI: {operationName} Starting...");
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        _stopwatches.Push(stopwatch);
    }

    private long StopPerfLog(string operationName)
    {
        if (_stopwatches.Count > 0)
        {
            var stopwatch = _stopwatches.Pop();
            stopwatch.Stop();
            _logger.LogInformation($"PI: {operationName} completed in {stopwatch.ElapsedMilliseconds} ms.");
            return stopwatch.ElapsedMilliseconds;
        }
        else
        {
            _logger.LogWarning($"PI: StopPerfLog called for {operationName} but no stopwatch was found.");
        }
        return 0;
    }

    private long GetTotalElapsedMilliseconds()
    {
        long totalElapsedMilliseconds = 0;
        foreach (var stopwatch in _stopwatches)
        {
            totalElapsedMilliseconds += stopwatch.ElapsedMilliseconds;
        }
        return totalElapsedMilliseconds;
    }
}
