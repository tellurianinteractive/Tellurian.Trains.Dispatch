using Tellurian.Trains.Dispatch.Server.Mapping;
using Tellurian.Trains.Dispatch.Server.Services;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Shared;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Server.Endpoints;

/// <summary>
/// Minimal API endpoints for train section actions.
/// </summary>
public static class TrainSectionEndpoints
{
    /// <summary>
    /// Maps train section endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapTrainSectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/train-sections")
            .WithTags("Train Sections");

        group.MapPost("/{id:int}/actions/{action}", ExecuteAction)
            .WithName("ExecuteAction")
            .WithSummary("Execute a dispatch or train action")
            .Produces<ActionResultData>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:int}/actions/pass/{controlPointIndex:int}", ExecutePassAction)
            .WithName("ExecutePassAction")
            .WithSummary("Execute a pass action through a signal-controlled place")
            .Produces<ActionResultData>()
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> ExecuteAction(
        int id,
        string action,
        IBroker broker,
        ICompositeStateProvider stateProvider,
        DispatchEventService eventService,
        CancellationToken cancellationToken)
    {
        // Find the train section
        var section = broker.TrainSections.FirstOrDefault(s => s.Id == id);
        if (section is null)
        {
            return Results.NotFound(new ActionResultData
            {
                Success = false,
                Error = $"Train section with id {id} not found"
            });
        }

        // Parse the action name
        if (!Enum.TryParse<DispatchAction>(action, ignoreCase: true, out var dispatchAction))
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = $"Invalid action: {action}"
            });
        }

        // Find a dispatcher that can perform this action
        var availableActions = GetAllAvailableActions(broker, section);
        var actionContext = availableActions.FirstOrDefault(a => a.Action == dispatchAction);

        if (actionContext is null)
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = $"Action '{action}' is not available for train section {id}"
            });
        }

        // Execute the action
        var result = await actionContext.ExecuteAndRecordAsync(stateProvider, cancellationToken);

        if (!result.HasValue)
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = result.Error
            });
        }

        // Publish event
        await PublishEventAsync(eventService, actionContext, section, cancellationToken);

        // Return the updated section
        var updatedActions = GetAllAvailableActions(broker, section);
        return Results.Ok(new ActionResultData
        {
            Success = true,
            TrainSection = section.ToData(updatedActions)
        });
    }

    private static async Task<IResult> ExecutePassAction(
        int id,
        int controlPointIndex,
        IBroker broker,
        ICompositeStateProvider stateProvider,
        DispatchEventService eventService,
        CancellationToken cancellationToken)
    {
        // Find the train section
        var section = broker.TrainSections.FirstOrDefault(s => s.Id == id);
        if (section is null)
        {
            return Results.NotFound(new ActionResultData
            {
                Success = false,
                Error = $"Train section with id {id} not found"
            });
        }

        // Get the control point at the specified index
        var controlPoints = section.DispatchStretch.IntermediateControlPoints.ToList();
        if (controlPointIndex < 0 || controlPointIndex >= controlPoints.Count)
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = $"Invalid control point index: {controlPointIndex}"
            });
        }

        var passTarget = controlPoints[controlPointIndex];

        // Find a dispatcher that can perform this pass action
        var availableActions = GetAllAvailableActions(broker, section);
        var actionContext = availableActions.FirstOrDefault(a =>
            a.Action == DispatchAction.Pass &&
            a.PassTarget?.Id == passTarget.Id);

        if (actionContext is null)
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = $"Pass action for control point '{passTarget.Name}' is not available"
            });
        }

        // Execute the action
        var result = await actionContext.ExecuteAndRecordAsync(stateProvider, cancellationToken);

        if (!result.HasValue)
        {
            return Results.BadRequest(new ActionResultData
            {
                Success = false,
                Error = result.Error
            });
        }

        // Publish event
        await PublishPassEventAsync(eventService, actionContext, section, passTarget, cancellationToken);

        // Return the updated section
        var updatedActions = GetAllAvailableActions(broker, section);
        return Results.Ok(new ActionResultData
        {
            Success = true,
            TrainSection = section.ToData(updatedActions)
        });
    }

    private static IEnumerable<ActionContext> GetAllAvailableActions(IBroker broker, TrainSection section)
    {
        // Get actions from all dispatchers that have actions for this section
        return broker.GetDispatchers()
            .SelectMany(d => broker.GetDepartureActionsFor(d, 100).Concat(broker.GetArrivalActionsFor(d, 100)))
            .Where(a => a.Section.Id == section.Id)
            .DistinctBy(a => (a.Action, a.PassTarget?.Id));
    }

    private static async Task PublishEventAsync(
        DispatchEventService eventService,
        ActionContext actionContext,
        TrainSection section,
        CancellationToken cancellationToken)
    {
        var train = section.Departure.Train;
        var affectedDispatcherIds = new List<int> { section.From.Id, section.To.Id };

        DispatchEvent evt = actionContext.IsTrainAction
            ? new TrainStateChangedEvent
            {
                EventType = DispatchEventTypes.TrainStateChanged,
                Timestamp = DateTimeOffset.UtcNow,
                TrainSectionId = section.Id,
                Train = train.ToDisplayString(),
                Action = actionContext.Action.ToString(),
                AffectedDispatcherIds = affectedDispatcherIds,
                PreviousState = train.PreviousState?.ToString() ?? "Unknown",
                NewState = train.State.ToString()
            }
            : new DispatchStateChangedEvent
            {
                EventType = DispatchEventTypes.DispatchStateChanged,
                Timestamp = DateTimeOffset.UtcNow,
                TrainSectionId = section.Id,
                Train = train.ToDisplayString(),
                Action = actionContext.Action.ToString(),
                AffectedDispatcherIds = affectedDispatcherIds,
                PreviousState = "Unknown", // Would need to track previous state
                NewState = section.State.ToString()
            };

        await eventService.PublishAsync(evt, cancellationToken);
    }

    private static async Task PublishPassEventAsync(
        DispatchEventService eventService,
        ActionContext actionContext,
        TrainSection section,
        SignalControlledPlace passTarget,
        CancellationToken cancellationToken)
    {
        var train = section.Departure.Train;
        var affectedDispatcherIds = new List<int> { section.From.Id, section.To.Id };
        if (passTarget.ControlledByDispatcherId > 0)
        {
            affectedDispatcherIds.Add(passTarget.ControlledByDispatcherId);
        }

        var evt = new ControlPointPassedEvent
        {
            EventType = DispatchEventTypes.ControlPointPassed,
            Timestamp = DateTimeOffset.UtcNow,
            TrainSectionId = section.Id,
            Train = train.ToDisplayString(),
            Action = actionContext.Action.ToString(),
            AffectedDispatcherIds = affectedDispatcherIds.Distinct().ToList(),
            ControlPointName = passTarget.Name,
            NewTrackStretchIndex = section.CurrentTrackStretchIndex
        };

        await eventService.PublishAsync(evt, cancellationToken);
    }
}
