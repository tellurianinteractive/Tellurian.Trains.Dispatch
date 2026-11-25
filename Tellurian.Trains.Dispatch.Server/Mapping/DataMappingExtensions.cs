using Tellurian.Trains.Dispatch.Shared;

namespace Tellurian.Trains.Dispatch.Server.Mapping;

/// <summary>
/// Extension methods for converting domain objects to Data objects with HATEOAS hrefs.
/// </summary>
public static class DataMappingExtensions
{
    private const string ApiBase = "/api/v1";

    /// <summary>
    /// Converts a StationDispatcher to a summary Data object with HATEOAS links.
    /// </summary>
    public static DispatcherSummaryData ToSummaryData(this StationDispatcher dispatcher)
        => new()
        {
            Id = dispatcher.Id,
            Signature = dispatcher.Signature,
            StationName = dispatcher.Name,
            Href = $"{ApiBase}/dispatchers/{dispatcher.Id}"
        };

    /// <summary>
    /// Converts a StationDispatcher to a full Data object with arrivals and departures.
    /// </summary>
    public static DispatcherData ToData(this StationDispatcher dispatcher)
    {
        // Get arrivals - train sections where this station is the destination
        var arrivalSections = dispatcher.ArrivalActions
            .Select(a => a.Section)
            .DistinctBy(s => s.Id)
            .ToList();

        // Get departures - train sections where this station is the origin
        var departureSections = dispatcher.DepartureActions
            .Select(a => a.Section)
            .DistinctBy(s => s.Id)
            .ToList();

        // Group actions by section for mapping
        var arrivalActionsBySection = dispatcher.ArrivalActions
            .GroupBy(a => a.Section.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        var departureActionsBySection = dispatcher.DepartureActions
            .GroupBy(a => a.Section.Id)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new DispatcherData
        {
            Id = dispatcher.Id,
            Signature = dispatcher.Signature,
            StationName = dispatcher.Name,
            PreferredLanguage = dispatcher.Station.PreferredLanguage,
            Self = $"{ApiBase}/dispatchers/{dispatcher.Id}",
            Events = $"{ApiBase}/events?dispatcherId={dispatcher.Id}",
            Arrivals = arrivalSections
                .Select(s => s.ToData(arrivalActionsBySection.GetValueOrDefault(s.Id, [])))
                .ToList(),
            Departures = departureSections
                .Select(s => s.ToData(departureActionsBySection.GetValueOrDefault(s.Id, [])))
                .ToList()
        };
    }

    /// <summary>
    /// Converts a TrainSection to a Data object with the specified available actions.
    /// </summary>
    public static TrainSectionData ToData(this TrainSection section, IEnumerable<ActionContext> actions)
    {
        var train = section.Departure.Train;
        return new()
        {
            Id = section.Id,
            Train = train.ToDisplayString(),
            OperatingDay = train.OperatingDay,
            Kind = section.Kind.ToString(),
            TrainState = train.State.ToString(),
            State = section.State.ToString(),
            From = section.From.Signature,
            To = section.To.Signature,
            DepartureTrack = section.Departure.Track.Number,
            ArrivalTrack = section.Arrival.Track.Number,
            ScheduledDeparture = FormatTime(section.Departure.Scheduled.DepartureTime),
            ScheduledArrival = FormatTime(section.Arrival.Scheduled.ArrivalTime),
            CurrentTrackStretch = section.CurrentTrackStretchIndex,
            TotalTrackStretches = section.TotalTrackStretches,
            Actions = actions.Select(a => a.ToData()).ToList()
        };
    }

    /// <summary>
    /// Converts an ActionContext to a Data object with HATEOAS href.
    /// </summary>
    public static ActionData ToData(this ActionContext action)
    {
        var actionName = action.Action.ToString().ToLowerInvariant();
        var href = action.Action == DispatchAction.Pass && action.PassTarget is not null
            ? $"{ApiBase}/train-sections/{action.Section.Id}/actions/pass/{GetPassTargetIndex(action)}"
            : $"{ApiBase}/train-sections/{action.Section.Id}/actions/{actionName}";

        return new ActionData(actionName, action.DisplayName, href);
    }

    /// <summary>
    /// Creates a display string for a train (e.g., "SJ IC 456").
    /// </summary>
    public static string ToDisplayString(this Trains.Train train)
        => $"{train.Company.Signature} {train.Identity.Prefix}{train.Identity.Number}";

    private static string FormatTime(TimeSpan time)
        => $"{time.Hours:D2}:{time.Minutes:D2}";

    private static int GetPassTargetIndex(ActionContext action)
    {
        if (action.PassTarget is null) return 0;

        // Find the index of the pass target in the intermediate control points
        var controlPoints = action.Section.IntermediateControlPoints.ToList();
        var index = controlPoints.FindIndex(cp => cp.Id == action.PassTarget.Id);
        return index >= 0 ? index : 0;
    }
}
