using Microsoft.AspNetCore.Http.HttpResults;
using System.Text.Json;
using Tellurian.Trains.Dispatch.Api.Services;
using Tellurian.Trains.Dispatch.Shared;

namespace Tellurian.Trains.Dispatch.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for Server-Sent Events (SSE).
/// </summary>
public static class EventEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Maps event endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/events", GetEvents)
            .WithName("GetEvents")
            .WithTags("Events")
            .WithSummary("Subscribe to real-time dispatch events via Server-Sent Events")
            .Produces<DispatchEvent>(StatusCodes.Status200OK, contentType: "text/event-stream");

        return app;
    }

    private static ServerSentEventsResult<string> GetEvents(
        int? dispatcherId,
        DispatchEventService eventService,
        CancellationToken cancellationToken)
    {
        return TypedResults.ServerSentEvents(
            ToSseStrings(eventService.GetEventsAsync(dispatcherId, cancellationToken)));
    }

    private static async IAsyncEnumerable<string> ToSseStrings(
        IAsyncEnumerable<DispatchEvent> events)
    {
        await foreach (var evt in events)
        {
            yield return JsonSerializer.Serialize(evt, JsonOptions);
        }
    }
}
