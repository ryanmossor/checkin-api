using CheckinApi.Config;
using CheckinApi.Interfaces;
using CheckinApi.Models;
using CheckinApi.Services;
using Serilog;
using Serilog.Events;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
     .Enrich.FromLogContext()
     .MinimumLevel.Debug()
     .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
     .MinimumLevel.Override("System.Net.Http", builder.Environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Warning)
     .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
     .WriteTo.Seq(builder.Configuration["Serilog:WriteTo:0:Args:serverUrl"] ?? string.Empty)
     .CreateLogger();

// configures Serilog as ONLY logging provider
builder.Host.UseSerilog(dispose: true);

// configures Serilog as one of multiple potential logging providers
// builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

builder.Services.AddHttpClient();

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

Constants.DataDir = builder.Configuration[nameof(Constants.DataDir)];
builder.Configuration.AddJsonFile(Constants.SecretsFile, optional: false, reloadOnChange: true);

builder.Services.AddSingleton(builder.Configuration.Get<CheckinSecrets>());

builder.Services.AddSingleton<ICheckinLists, CheckinLists>();

builder.Services.AddSingleton(_ => new StravaAuthService(
    _.GetRequiredService<CheckinSecrets>(), new HttpClient(), _.GetRequiredService<ILogger<StravaAuthService>>()));

builder.Services.AddSingleton<IActivityService, StravaService>(_ => new StravaService(
    _.GetRequiredService<HttpClient>(),
    _.GetRequiredService<CheckinSecrets>(),
    _.GetRequiredService<StravaAuthService>(),
    _.GetRequiredService<ILogger<StravaService>>()));

builder.Services.AddSingleton(_ => new FitbitAuthService(
    _.GetRequiredService<CheckinSecrets>(), new HttpClient(), _.GetRequiredService<ILogger<FitbitAuthService>>()));

builder.Services.AddSingleton<IHealthTrackingService, FitbitService>(_ => new FitbitService(
    _.GetRequiredService<HttpClient>(),
    _.GetRequiredService<CheckinSecrets>(),
    _.GetRequiredService<FitbitAuthService>(),
    _.GetRequiredService<ILogger<FitbitService>>()));

builder.Services.AddSingleton<ICheckinQueueProcessor, CheckinQueueProcessor>();

builder.Services.AddControllers();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
    
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseStaticFiles(); // enables custom Swagger CSS
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
    options.InjectStylesheet("/swagger-ui/SwaggerDark.css");
    options.RoutePrefix = string.Empty;
    
    if (!app.Environment.IsDevelopment()) 
        options.SupportedSubmitMethods(SubmitMethod.Get); 
});

// commented out to access via local network since dev cert untrusted
// app.UseHttpsRedirection();

app.UseAuthorization();
app.MapControllers();

try
{
    app.Run();
}
catch (TaskCanceledException) { }
catch (Exception ex)
{
    Log.Logger.Error(ex, "Exception starting check-in API");
}
finally
{
    Log.CloseAndFlush();
}
