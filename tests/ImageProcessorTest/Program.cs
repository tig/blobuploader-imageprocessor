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
            FileName = "test-image",
            Extension = "jpg",
            OriginalWidth = 3840,
            OriginalHeight = 2160,
            SizedWidth = 1920,
            SizedHeight = 1080,
            ThumbnailWidth = 300,
            ThumbnailHeight = 300,
            BlobConnectionString = "YourBlobConnectionString",
            BlobContainer = "test-container"
        };

        var jsonRequest = JsonSerializer.Serialize(request);

        using var client = new HttpClient();
        var response = await client.PostAsync(functionUrl, new StringContent(jsonRequest, Encoding.UTF8, "application/json"));

        Console.WriteLine(await response.Content.ReadAsStringAsync());
    }
}
