namespace Tellurian.Trains.Dispatch.Shared;

/// <summary>
/// Represents a train section (movement between two stations) with its current state
/// and available actions. Used in dispatcher arrivals/departures lists.
/// </summary>
public record TrainSectionData
{
    /// <summary>
    /// Unique identifier for the train section.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// The train identity (e.g., "SJ IC 456").
    /// </summary>
    public required string Train { get; init; }

    /// <summary>
    /// The operational state of the train (Planned, Manned, Running, etc.).
    /// </summary>
    public required string TrainState { get; init; }

    /// <summary>
    /// The dispatch state (None, Requested, Accepted, Departed, etc.).
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// The departure station signature.
    /// </summary>
    public required string From { get; init; }

    /// <summary>
    /// The arrival station signature.
    /// </summary>
    public required string To { get; init; }

    /// <summary>
    /// The track at the departure station.
    /// </summary>
    public required string DepartureTrack { get; init; }

    /// <summary>
    /// The track at the arrival station.
    /// </summary>
    public required string ArrivalTrack { get; init; }

    /// <summary>
    /// Scheduled departure time (HH:mm format).
    /// </summary>
    public required string ScheduledDeparture { get; init; }

    /// <summary>
    /// Scheduled arrival time (HH:mm format).
    /// </summary>
    public required string ScheduledArrival { get; init; }

    /// <summary>
    /// Current track stretch index (0-based). Indicates train progress along the route.
    /// </summary>
    public required int CurrentTrackStretch { get; init; }

    /// <summary>
    /// Total number of track stretches to traverse.
    /// </summary>
    public required int TotalTrackStretches { get; init; }

    /// <summary>
    /// Available actions for this train section. HATEOAS links - clients follow hrefs.
    /// </summary>
    public required IReadOnlyList<ActionData> Actions { get; init; }
}
