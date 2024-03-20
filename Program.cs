using CheckinApi;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
     .Enrich.FromLogContext()
     .MinimumLevel.Debug()
     .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
     .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Warning)
     .WriteTo.Seq(builder.Configuration["Serilog:WriteTo:0:Args:serverUrl"] ?? string.Empty)
     .CreateLogger();

// configures Serilog as ONLY logging provider
builder.Host.UseSerilog(dispose: true);

// configures Serilog as one of multiple potential logging providers
// builder.Services.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));

builder.Services.AddSingleton<ICheckinLists, CheckinLists>();
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
