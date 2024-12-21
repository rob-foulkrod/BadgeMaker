using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using Azure.Storage.Blobs;




var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
builder.Services
    .AddApplicationInsightsTelemetryWorkerService();

builder.Services.AddSingleton(x =>
{
    var configuration = builder.Configuration;
    var credential = new DefaultAzureCredential();

    // Get the URI from configuration: AzureWebJobsStorage__blobUri
    var uri = configuration["AzureWebJobsStorage__blobUri"];
    if (string.IsNullOrEmpty(uri))
    {
        uri = configuration["AzureWebJobsStorage:blobUri"];
    }
    if (string.IsNullOrEmpty(uri))
    {
        uri = configuration["AzureWebJobsStorage"];
    }

    var blobServiceClient = new BlobServiceClient(new Uri(uri), credential);
    return blobServiceClient;
});


builder.Services.ConfigureFunctionsApplicationInsights();

builder.Build().Run();


