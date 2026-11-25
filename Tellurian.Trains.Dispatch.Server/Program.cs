using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Server.Endpoints;
using Tellurian.Trains.Dispatch.Server.Services;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<DispatchEventService>();
builder.Services.AddSingleton<ICompositeStateProvider, InMemoryCompositeStateProvider>();
builder.Services.AddTimeProvider(builder.Configuration);
builder.Services.AddBrokerDataProvider(builder.Configuration);
builder.Services.AddSingleton<IBroker, Broker>();

// Add CORS for local development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Initialize the broker
var broker = app.Services.GetRequiredService<IBroker>();
await broker.InitAsync(isRestart: false);

// Configure middleware
app.UseCors();

// Map endpoints
app.MapDispatcherEndpoints();
app.MapTrainSectionEndpoints();
app.MapEventEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithName("HealthCheck")
    .WithTags("Health");

app.Run();
