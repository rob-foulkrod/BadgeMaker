using BadgeMaker.Components;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;
using BadgeMaker.Components.Services;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();
builder.Configuration.AddUserSecrets<Program>();

var openAIConfig = builder.Configuration.GetSection("openai").Get<OpenAIConfig>();

if (openAIConfig == null)
{
    SystemMessages.ConfigurationWarnings.Add("OpenAI configuration is missing");
    openAIConfig = new OpenAIConfig();
}

builder.Services.AddSingleton<OpenAIConfig>(openAIConfig);

var serviceBusConfig = builder.Configuration.GetSection("serviceBus").Get<ServiceBusConfig>();
if (serviceBusConfig == null)
    {
    SystemMessages.ConfigurationWarnings.Add("ServiceBus configuration is missing");
    serviceBusConfig = new ServiceBusConfig();
}

builder.Services.AddSingleton<ServiceBusConfig>(serviceBusConfig);

var storageBlobEndpoint = builder.Configuration["storage:blobEndpoint"];
if (!string.IsNullOrEmpty(storageBlobEndpoint))
{
    builder.Services.AddSingleton(_ =>
        new BlobServiceClient(new Uri(storageBlobEndpoint), new DefaultAzureCredential()));
}
else
{
    SystemMessages.ConfigurationWarnings.Add("Storage configuration is missing — blob upload will not work");
    builder.Services.AddSingleton(_ => new BlobServiceClient("UseDevelopmentStorage=true"));
}

builder.Services.AddScoped<BadgeGeneratorViewModel>();
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
builder.Services.AddSingleton<IServiceBusService, ServiceBusService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();


builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
