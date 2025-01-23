using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using DotNetEnv;

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

        var imagePath = Path.GetFullPath("../../tests/jpg_test.jpg");

        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"File not found: {imagePath}");
            return;
        }

        using var client = new HttpClient();
        if (!string.IsNullOrEmpty(apiKey))
        {
            client.DefaultRequestHeaders.Add("x-functions-key", apiKey);
        }

        using var multipartContent = new MultipartFormDataContent();

        // Add binary file content
        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(imagePath));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        multipartContent.Add(fileContent, "file", Path.GetFileName(imagePath));

        // Add metadata as form-data fields
        multipartContent.Add(new StringContent("/uploads/77777/"), "SubDirectory");
        multipartContent.Add(new StringContent("true"), "UseHashForFileName");
        multipartContent.Add(new StringContent("true"), "DeDupe");
        multipartContent.Add(new StringContent("jpg-test"), "FileName");
        multipartContent.Add(new StringContent("jpg"), "Extension");
        multipartContent.Add(new StringContent("3840"), "OriginalWidth");
        multipartContent.Add(new StringContent("2160"), "OriginalHeight");
        multipartContent.Add(new StringContent("1920"), "SizedWidth");
        multipartContent.Add(new StringContent("1080"), "SizedHeight");
        multipartContent.Add(new StringContent("300"), "ThumbnailWidth");
        multipartContent.Add(new StringContent("300"), "ThumbnailHeight");
        multipartContent.Add(new StringContent(blobConnectionString), "BlobConnectionString");
        multipartContent.Add(new StringContent("test-container"), "BlobContainer");

        Console.WriteLine($"Request Content-Type: {multipartContent.Headers.ContentType}");
        foreach (var content in multipartContent)
        {
            Console.WriteLine($"Content: {content.Headers.ContentDisposition}");
        }

        client.Timeout = TimeSpan.FromMinutes(1);

        // TODO: Delete existing blob before running this test

        // Send the request
        Console.WriteLine($"Sending request to {functionUrl}...");
        var response = await client.PostAsync(functionUrl, multipartContent);

        Console.WriteLine($"Response status code: {response.StatusCode}");
        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error Response: {await response.Content.ReadAsStringAsync()}");
        }

        Console.WriteLine(await response.Content.ReadAsStringAsync());

        // TODO: Do it again with the same file to test dedupe

        // TODO: Do it again with a different file to test dedupe

        // TODO: Test other file types

        Console.WriteLine("Done!");
    }
}
