using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using EnergyTracker.Server.Data;
using EnergyTracker.Server.Jobs;
using Hangfire;
using Hangfire.SqlServer;
using Hangfire.Console;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<DatabaseContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth0:Authority"];
    options.Audience = builder.Configuration["Auth0:ApiIdentifier"];
});

// Add Hangfire services.
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseConsole()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("HangfireConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

// Add the processing server as IHostedService
builder.Services.AddHangfireServer();

builder.Services.AddMvc();
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EnergyTracker API", Version = "v1" });
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme 
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(builder.Configuration["Auth0:Authority"] + "/oauth/authorize"),
                TokenUrl = new Uri(builder.Configuration["Auth0:Authority"] + "/oauth/token"),
                Scopes = new Dictionary<string, string>
                {
                }
            }
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// To allow request from different ports
app.UseCors(config =>
{
    config.AllowAnyOrigin();
    config.AllowAnyMethod();
    config.AllowAnyHeader();
});

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard();

RecurringJob.AddOrUpdate("KSE-data-download", () => new KseDataDownloadJob(null).Execute(null), builder.Configuration.GetValue<string>("Hangfire:KseDataDownloadJob"));
RecurringJob.AddOrUpdate("weather-data-download", () => new WeatherDataDownloadJob(null).Execute(null), builder.Configuration.GetValue<string>("Hangfire:WeatherDataDownloadJob"));
RecurringJob.AddOrUpdate("forecast-data-download", () => new ForecastDataDownloadJob(null).Execute(null, builder.Configuration.GetValue<string>("Secrets:ForecastApiKey")), builder.Configuration.GetValue<string>("Hangfire:ForecastDataDownloadJob"));

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
});

app.MapRazorPages();
app.MapControllers();

//app.MapBlazorHub();
app.MapHub<EnergyTracker.Server.Hubs.ProcessHub>("/hub/ml");
app.MapFallbackToFile("index.html");

app.Run();
