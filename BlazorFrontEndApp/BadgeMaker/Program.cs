using BadgeMaker.Components;
using BadgeMaker.Components.Interfaces;
using BadgeMaker.Components.Models;
using BadgeMaker.Components.Services;
using Microsoft.FluentUI.AspNetCore.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();
builder.Configuration.AddUserSecrets<Program>();

var openApiConfig = builder.Configuration.GetSection("openai").Get<OpenAIConfig>();

if (openApiConfig == null)
{
    SystemMessages.ConfigurationWarnings.Add("OpenApi configuration is missing");
    openApiConfig = new OpenAIConfig();
}

builder.Services.AddSingleton<OpenAIConfig>(openApiConfig);

var serviceBusConfig = builder.Configuration.GetSection("serviceBus").Get<ServiceBusConfig>();
if (serviceBusConfig == null)
    {
    SystemMessages.ConfigurationWarnings.Add("ServiceBus configuration is missing");
    serviceBusConfig = new ServiceBusConfig();
}

builder.Services.AddSingleton<ServiceBusConfig>(serviceBusConfig);
builder.Services.AddSingleton<BadgeGeneratorViewModel>();
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
builder.Services.AddSingleton<IServiceBusService, ServiceBusService>();


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
