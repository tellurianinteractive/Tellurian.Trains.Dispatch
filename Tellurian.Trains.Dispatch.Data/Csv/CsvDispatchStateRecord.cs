namespace Tellurian.Trains.Dispatch.Data.Csv;

/// <summary>
/// Type of change recorded in the dispatch state CSV file.
/// </summary>
public enum DispatchStateChangeType
{
    /// <summary>Dispatch state changed (Requested, Accepted, Departed, Arrived, etc.)</summary>
    State,
    /// <summary>Train passed a signal-controlled place.</summary>
    Pass
}

/// <summary>
/// Represents a single record in the dispatch state CSV file.
/// </summary>
/// <remarks>
/// CSV format:
/// Timestamp,ChangeType,SectionId,State,TrackStretchIndex,SignalPlaceId
/// </remarks>
public class CsvDispatchStateRecord
{
    /// <summary>
    /// ISO 8601 timestamp of when the change occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Type of change: State or Pass.
    /// </summary>
    public DispatchStateChangeType ChangeType { get; set; }

    /// <summary>
    /// Train section identifier.
    /// </summary>
    public int SectionId { get; set; }

    /// <summary>
    /// New dispatch state (for State changes).
    /// </summary>
    public DispatchState? State { get; set; }

    /// <summary>
    /// Current track stretch index (set when Departed, updated on Pass).
    /// </summary>
    public int? TrackStretchIndex { get; set; }

    /// <summary>
    /// Signal-controlled place ID (for Pass changes).
    /// </summary>
    public int? SignalPlaceId { get; set; }

    /// <summary>
    /// Creates a record for a dispatch state change.
    /// </summary>
    public static CsvDispatchStateRecord ForStateChange(int sectionId, DispatchState state, int? trackStretchIndex = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = DispatchStateChangeType.State,
        SectionId = sectionId,
        State = state,
        TrackStretchIndex = trackStretchIndex
    };

    /// <summary>
    /// Creates a record for a pass event.
    /// </summary>
    public static CsvDispatchStateRecord ForPass(int sectionId, int signalPlaceId, int newTrackStretchIndex) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        ChangeType = DispatchStateChangeType.Pass,
        SectionId = sectionId,
        SignalPlaceId = signalPlaceId,
        TrackStretchIndex = newTrackStretchIndex
    };
}
