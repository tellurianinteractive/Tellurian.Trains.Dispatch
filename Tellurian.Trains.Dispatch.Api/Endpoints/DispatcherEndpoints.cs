using Tellurian.Trains.Dispatch.Api.Mapping;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Shared;

namespace Tellurian.Trains.Dispatch.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for dispatcher operations.
/// </summary>
public static class DispatcherEndpoints
{
    /// <summary>
    /// Maps dispatcher endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapDispatcherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/dispatchers")
            .WithTags("Dispatchers");

        group.MapGet("", GetDispatchers)
            .WithName("GetDispatchers")
            .WithSummary("List all station dispatchers")
            .Produces<IEnumerable<DispatcherSummaryData>>();

        group.MapGet("/{id:int}", GetDispatcher)
            .WithName("GetDispatcher")
            .WithSummary("Get a dispatcher with arrivals and departures")
            .Produces<DispatcherData>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static IResult GetDispatchers(IBroker broker)
    {
        var dispatchers = broker.GetDispatchers()
            .OfType<StationDispatcher>()
            .Select(d => d.ToSummaryData())
            .ToList();

        return Results.Ok(dispatchers);
    }

    private static IResult GetDispatcher(int id, IBroker broker)
    {
        var dispatcher = broker.GetDispatchers()
            .OfType<StationDispatcher>()
            .FirstOrDefault(d => d.Id == id);

        if (dispatcher is null)
        {
            return Results.NotFound(new { error = $"Dispatcher with id {id} not found" });
        }

        return Results.Ok(dispatcher.ToData());
    }
}
