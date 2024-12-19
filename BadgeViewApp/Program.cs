using Microsoft.Extensions.Configuration.AzureAppConfiguration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAzureAppConfiguration();

// Add my own config file
builder.Configuration.AddJsonFile("appsettings-jf.json", true);

// Add App Configuration to application
var appConfigurationConnectionString = builder.Configuration["AppConfiguration"];

//if (string.IsNullOrEmpty(appConfigurationConnectionString))
//{
//    throw new Exception("AppConfiguration connection string is missing");
//}

//builder.Configuration.AddAzureAppConfiguration(options =>
//{
//    options
//        .Connect(appConfigurationConnectionString)
//        .Select("BadgeMaker:BadgeView:*")
//        .ConfigureRefresh(refresh =>
//        {
//            refresh.Register("BadgeMaker:BadgeView:Message", refreshAll: true)
//                .SetCacheExpiration(TimeSpan.FromSeconds(5));
//        });
//});

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSingleton<BadgeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseAzureAppConfiguration();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
