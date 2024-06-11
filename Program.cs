using BadgeMaker.Components;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();
builder.Configuration.AddUserSecrets<Program>();


var openApiConfig = builder.Configuration.GetSection("openai").Get<OpenApiConfig>();

if (openApiConfig == null)
{
    SystemMessages.ConfigurationWarnings.Add("OpenApi configuration is missing");
    openApiConfig = new OpenApiConfig();
}



builder.Services.AddSingleton<OpenApiConfig>(openApiConfig);

var serviceBusConfig = builder.Configuration.GetSection("serviceBus").Get<ServiceBusConfig>();
if (serviceBusConfig == null)
    {
    SystemMessages.ConfigurationWarnings.Add("ServiceBus configuration is missing");
    serviceBusConfig = new ServiceBusConfig();
}

builder.Services.AddSingleton<ServiceBusConfig>(serviceBusConfig);
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



public class OpenApiConfig
{
    public string apiKey { get; set; }
    public string deployment { get; set; }
    public string endpoint { get; set; }
    public bool IsConfigured => !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(deployment) && !string.IsNullOrEmpty(endpoint);

}

public class ServiceBusConfig
{
    public string connectionString { get; set; }
    public string queueName { get; set; }
    public bool IsConfigured => !string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(queueName);
}

class SystemMessages
{
    public static List<string> ConfigurationWarnings { get; set; } = new List<string>();

}