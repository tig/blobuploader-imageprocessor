using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using ImageProcessor.Services;

[assembly: FunctionsStartup(typeof(ImageProcessor.Startup))]

namespace ImageProcessor
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            Console.WriteLine("Configuring services...");
            builder.Services.AddSingleton<ImageProcessingService>();
            // Register other services here
        }
    }
}