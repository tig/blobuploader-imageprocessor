using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DotNetEnv;
using Azure.Storage.Blobs;

class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run <environment>");
            Console.WriteLine("Environments: local, azure");
            return;
        }

        string environment = args[0].ToLower();
        string functionUrl;
        string blobConnectionString;
        string apiKey = null;

        if (environment == "local")
        {
            functionUrl = "http://localhost:7071/api/ProcessImage";
            blobConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;";
        }
        else if (environment == "azure")
        {
            // Load environment variables from .env file in the tests/ImageProcessorTest folder
            Env.Load("../../tests/ImageProcessorTest/vars.env");
            functionUrl = Environment.GetEnvironmentVariable("AZURE_FUNCTION_URL");
            blobConnectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTION_STRING");
            apiKey = Environment.GetEnvironmentVariable("AZURE_FUNCTION_API_KEY");

            if (string.IsNullOrEmpty(functionUrl) || string.IsNullOrEmpty(blobConnectionString) || string.IsNullOrEmpty(apiKey))
            {
                Console.WriteLine("Missing required environment variables.");
                return;
            }
        }
        else
        {
            Console.WriteLine("Invalid environment specified. Use 'local' or 'azure'.");
            return;
        }

        string[] testFiles = { "jpg_test.jpg", "gif_test.gif", "earth.gif" };

        using var client = new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("x-functions-key", apiKey);
        }

        client.Timeout = TimeSpan.FromMinutes(1);

        // Delete existing blobs before running this test
        await DeleteAllBlobsAsync(blobConnectionString, "test-container");

        foreach (var file in testFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var extension = Path.GetExtension(file).TrimStart('.');

            var imagePath = Path.GetFullPath($"../../tests/{file}");

            if (!File.Exists(imagePath))
            {
                Console.WriteLine($"File not found: {imagePath}");
                continue;
            }

            // Send the request
            await SendRequest(client, functionUrl, imagePath, fileName, extension, blobConnectionString);

            // Do it again with the same file to test dedupe
            await SendRequest(client, functionUrl, imagePath, fileName, extension, blobConnectionString);

            // Do it again with a different file to test dedupe
            var differentFilePath = Path.GetFullPath($"../../tests/different_{file}");
            if (File.Exists(differentFilePath))
            {
                await SendRequest(client, functionUrl, differentFilePath, fileName, extension, blobConnectionString);
            }
        }

        Console.WriteLine("Done!");
    }

    private static async Task DeleteAllBlobsAsync(string blobConnectionString, string containerName)
    {
        var blobServiceClient = new BlobServiceClient(blobConnectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            var blobClient = containerClient.GetBlobClient(blobItem.Name);
            await blobClient.DeleteIfExistsAsync();
            Console.WriteLine($"Deleted blob: {blobItem.Name}");
        }
    }
    private static async Task DeleteBlobIfExists(HttpClient client, string functionUrl, string fileName, string extension, string blobConnectionString)
    {
        // Implement the logic to delete the existing blob if it exists
        // This is a placeholder for the actual implementation
        Console.WriteLine($"Deleting existing blob for {fileName}.{extension}...");

        // We have the blobConnectionString, so we can use it to delete the blob
        var deleteUrl = $"{functionUrl}?fileName={fileName}&extension={extension}&blobConnectionString={blobConnectionString}";
        var response = await client.DeleteAsync(deleteUrl);

        Console.WriteLine($"Delete response status code: {response.StatusCode}");        
    }

    private static async Task SendRequest(HttpClient client, string functionUrl, string imagePath, string fileName, string extension, string blobConnectionString)
    {
        using var multipartContent = new MultipartFormDataContent();

        // Add binary file content
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(imagePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue($"image/{extension}");
        multipartContent.Add(fileContent, "file", Path.GetFileName(imagePath));

        // Add metadata as form-data fields
        multipartContent.Add(new StringContent("/uploads/77777/"), "SubDirectory");
        multipartContent.Add(new StringContent("true"), "UseHashForFileName");
        multipartContent.Add(new StringContent("true"), "DeDupe");
        multipartContent.Add(new StringContent(fileName), "FileName");
        multipartContent.Add(new StringContent(extension), "Extension");
        multipartContent.Add(new StringContent("1280"), "OriginalWidth");
        multipartContent.Add(new StringContent("1024"), "OriginalHeight");
        multipartContent.Add(new StringContent("800"), "SizedWidth");
        multipartContent.Add(new StringContent("600"), "SizedHeight");
        multipartContent.Add(new StringContent("150"), "ThumbnailWidth");
        multipartContent.Add(new StringContent("150"), "ThumbnailHeight");
        multipartContent.Add(new StringContent(blobConnectionString), "BlobConnectionString");
        multipartContent.Add(new StringContent("test-container"), "BlobContainer");

        Console.WriteLine($"Request Content-Type: {multipartContent.Headers.ContentType}");
        // foreach (var content in multipartContent)
        // {
        //     Console.WriteLine($"Content: {content.Headers.ContentDisposition}");
        // }

        Console.WriteLine($"Sending request to {functionUrl}...");
        var response = await client.PostAsync(functionUrl, multipartContent);

        Console.WriteLine($"Response status code: {response.StatusCode}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error Response: {await response.Content.ReadAsStringAsync()}");
        }

        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
}