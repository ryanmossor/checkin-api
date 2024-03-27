using CheckinApi.Config;
using CheckinApi.Interfaces;
using CheckinApi.Services;
using Serilog;
using Serilog.Events;
using Swashbuckle.AspNetCore.SwaggerUI;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

var config = builder.Configuration.Get<CheckinConfig>()!;
builder.Services.AddSingleton(config);

builder.Configuration.AddJsonFile(config.SecretsFile, optional: false, reloadOnChange: true);
builder.Services.AddSingleton(builder.Configuration.Get<CheckinSecrets>());

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

builder.Services.AddSingleton<ICheckinLists, CheckinLists>();

builder.Services.AddSingleton(s => new StravaAuthService(
    s.GetRequiredService<CheckinSecrets>(),
    s.GetRequiredService<HttpClient>(),
    s.GetRequiredService<ILogger<StravaAuthService>>(),
    s.GetRequiredService<CheckinConfig>()));

builder.Services.AddSingleton<IActivityService, StravaService>(s => new StravaService(
    s.GetRequiredService<HttpClient>(),
    s.GetRequiredService<CheckinSecrets>(),
    s.GetRequiredService<StravaAuthService>(),
    s.GetRequiredService<ILogger<StravaService>>()));

builder.Services.AddSingleton(s => new FitbitAuthService(
    s.GetRequiredService<CheckinSecrets>(),
    s.GetRequiredService<HttpClient>(),
    s.GetRequiredService<ILogger<FitbitAuthService>>(),
    s.GetRequiredService<CheckinConfig>()));

builder.Services.AddSingleton<IHealthTrackingService, FitbitService>(s => new FitbitService(
    s.GetRequiredService<HttpClient>(),
    s.GetRequiredService<CheckinSecrets>(),
    s.GetRequiredService<FitbitAuthService>(),
    s.GetRequiredService<ILogger<FitbitService>>()));

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
