using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// Type of change recorded in the train state CSV file.
/// </summary>
public enum TrainStateChangeType
{
    /// <summary>Train state changed (Planned, Manned, Running, etc.)</summary>
    State,
    /// <summary>Observed arrival time recorded.</summary>
    ObservedArrival,
    /// <summary>Observed departure time recorded.</summary>
    ObservedDeparture,
    /// <summary>Track assignment changed.</summary>
    TrackChange
}

/// <summary>
/// Represents a single record in the train state CSV file.
/// </summary>
/// <remarks>
/// CSV format:
/// Timestamp,ChangeType,TrainId,CallId,State,Time,NewTrack
/// </remarks>
public class CsvTrainStateRecord
{
    /// <summary>
    /// ISO 8601 timestamp of when the change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Type of change: State, ObservedArrival, ObservedDeparture, or TrackChange.
    /// </summary>
    public TrainStateChangeType ChangeType { get; set; }

    /// <summary>
    /// Train identifier (for all change types).
    /// </summary>
    public int TrainId { get; set; }

    /// <summary>
    /// Call identifier (for ObservedArrival, ObservedDeparture, and TrackChange).
    /// </summary>
    public int? CallId { get; set; }

    /// <summary>
    /// New train state (for State changes).
    /// </summary>
    public TrainState? State { get; set; }

    /// <summary>
    /// Observed time as TimeSpan (for ObservedArrival and ObservedDeparture).
    /// </summary>
    public TimeSpan? Time { get; set; }

    /// <summary>
    /// New track number (for TrackChange).
    /// </summary>
    public string? NewTrack { get; set; }

    /// <summary>
    /// Creates a record for a train state change.
    /// </summary>
    public static CsvTrainStateRecord ForStateChange(int trainId, TrainState state) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = TrainStateChangeType.State,
        TrainId = trainId,
        State = state
    };

    /// <summary>
    /// Creates a record for an observed arrival time.
    /// </summary>
    public static CsvTrainStateRecord ForObservedArrival(int trainId, int callId, TimeSpan arrivalTime) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = TrainStateChangeType.ObservedArrival,
        TrainId = trainId,
        CallId = callId,
        Time = arrivalTime
    };

    /// <summary>
    /// Creates a record for an observed departure time.
    /// </summary>
    public static CsvTrainStateRecord ForObservedDeparture(int trainId, int callId, TimeSpan departureTime) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = TrainStateChangeType.ObservedDeparture,
        TrainId = trainId,
        CallId = callId,
        Time = departureTime
    };

    /// <summary>
    /// Creates a record for a track change.
    /// </summary>
    public static CsvTrainStateRecord ForTrackChange(int trainId, int callId, string newTrack) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = TrainStateChangeType.TrackChange,
        TrainId = trainId,
        CallId = callId,
        NewTrack = newTrack
    };
}
