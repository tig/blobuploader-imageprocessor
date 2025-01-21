using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using ImageProcessor;

class Program
{
    static async Task Main(string[] args)
    {
        var functionUrl = "http://localhost:7071/api/ProcessImage";
        var imagePath = "../../tests/jpg_test.jpg";

        var imageBase64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath));

        var request = new
        {
            ImageBase64 = imageBase64,
            UploadPath = "/uploads/777/",
            UseHashForFileName = true,
            DeDupe = true,
            FileName = "jpg-test",
            Extension = "jpg",
            OriginalWidth = 3840,
            OriginalHeight = 2160,
            SizedWidth = 1920,
            SizedHeight = 1080,
            ThumbnailWidth = 300,
            ThumbnailHeight = 300,
            BlobConnectionString = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:10001/devstoreaccount1;TableEndpoint=http://127.0.0.1:10002/devstoreaccount1;",
            BlobContainer = "test-container"
        };


        var jsonRequest = JsonSerializer.Serialize(request);

        using var client = new HttpClient();

        Console.WriteLine($"Sending request...");
        var response = await client.PostAsync(functionUrl, new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

        Console.WriteLine(await response.Content.ReadAsStringAsync());
        

        Console.WriteLine("Done!");
    }
}
