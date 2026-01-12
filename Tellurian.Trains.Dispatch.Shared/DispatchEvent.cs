namespace Tellurian.Trains.Dispatch.Shared;

/// <summary>
/// SSE event types for real-time updates.
/// </summary>
public static class DispatchEventTypes
{
    public const string DispatchStateChanged = "DispatchStateChanged";
    public const string TrainStateChanged = "TrainStateChanged";
    public const string ControlPointPassed = "ControlPointPassed";
    public const string TrainSectionCleared = "TrainSectionCleared";
}

/// <summary>
/// Base record for SSE events. All events include timestamp and affected dispatchers.
/// </summary>
public record DispatchEvent
{
    /// <summary>
    /// The type of event (used as SSE event type).
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// ID of the train section that changed.
    /// </summary>
    public required int TrainSectionId { get; init; }

    /// <summary>
    /// Train identity (e.g., "SJ IC 456").
    /// </summary>
    public required string Train { get; init; }

    /// <summary>
    /// The action that was performed.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// IDs of dispatchers affected by this event.
    /// Used for client-side filtering.
    /// </summary>
    public required IReadOnlyList<int> AffectedDispatcherIds { get; init; }
}

/// <summary>
/// Event for dispatch state changes (Request, Accept, Depart, Arrive, etc.).
/// </summary>
public record DispatchStateChangedEvent : DispatchEvent
{
    /// <summary>
    /// The previous dispatch state.
    /// </summary>
    public required string PreviousState { get; init; }

    /// <summary>
    /// The new dispatch state.
    /// </summary>
    public required string NewState { get; init; }
}

/// <summary>
/// Event for train state changes (Manned, Running, Canceled, etc.).
/// </summary>
public record TrainStateChangedEvent : DispatchEvent
{
    /// <summary>
    /// The previous train state.
    /// </summary>
    public required string PreviousState { get; init; }

    /// <summary>
    /// The new train state.
    /// </summary>
    public required string NewState { get; init; }
}

/// <summary>
/// Event for train passing a signal-controlled place.
/// </summary>
public record ControlPointPassedEvent : DispatchEvent
{
    /// <summary>
    /// Name of the control point that was passed.
    /// </summary>
    public required string ControlPointName { get; init; }

    /// <summary>
    /// New track stretch index after passing.
    /// </summary>
    public required int NewTrackStretchIndex { get; init; }
}
