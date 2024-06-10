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
    SystemMessages.ConfigurationWarning = "OpenApi configuration is missing";
    openApiConfig = new OpenApiConfig();
}


builder.Services.AddSingleton<OpenApiConfig>(openApiConfig);



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

}


class SystemMessages
{
    public static string? ConfigurationWarning { get; set; }

}