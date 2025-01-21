using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

Console.WriteLine($"Starting up...FunctionsApplication.CreateBuilder: {args.Length}");
// Log all the args
// for (int i = 0; i < args.Length; i++)
// {
//     Console.WriteLine($"args[{i}]: {args[i]}");
// }
var builder = FunctionsApplication.CreateBuilder(args);

Console.WriteLine("Configuring services...ConfigureFunctionsWebApplication");
builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();


Console.WriteLine("Building host...");
builder.Build().Run();
