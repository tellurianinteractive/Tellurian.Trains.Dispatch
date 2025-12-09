using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Json;

/// <summary>
/// Root container for broker state serialized to/from JSON.
/// This captures the runtime state changes that occur during dispatching.
/// </summary>
/// <remarks>
/// <para>
/// State is stored separately from layout and train data to enable:
/// <list type="bullet">
/// <item><description>Small state files containing only changed values</description></item>
/// <item><description>Reuse of layout/train data across multiple sessions</description></item>
/// <item><description>Easy reset by deleting the state file</description></item>
/// </list>
/// </para>
/// </remarks>
public class JsonBrokerState
{
    /// <summary>
    /// Timestamp when state was saved.
    /// </summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>
    /// State of each train.
    /// </summary>
    public List<JsonTrainState> TrainStates { get; set; } = [];

    /// <summary>
    /// State of each train section.
    /// </summary>
    public List<JsonTrainSectionState> SectionStates { get; set; } = [];

    /// <summary>
    /// Observed times and track changes for train station calls.
    /// </summary>
    public List<JsonCallState> CallStates { get; set; } = [];
}

/// <summary>
/// JSON representation of a train's state.
/// </summary>
public class JsonTrainState
{
    /// <summary>
    /// Train ID.
    /// </summary>
    public int TrainId { get; set; }

    /// <summary>
    /// Current train state.
    /// </summary>
    public TrainState State { get; set; }
}

/// <summary>
/// JSON representation of a train section's dispatch state.
/// </summary>
public class JsonTrainSectionState
{
    /// <summary>
    /// Section ID.
    /// </summary>
    public int SectionId { get; set; }

    /// <summary>
    /// Current dispatch state.
    /// </summary>
    public DispatchState State { get; set; }

    /// <summary>
    /// Index of current track stretch (for multi-stretch sections).
    /// </summary>
    public int CurrentTrackStretchIndex { get; set; }
}

/// <summary>
/// JSON representation of observed times and track changes for a train station call.
/// </summary>
public class JsonCallState
{
    /// <summary>
    /// Call ID.
    /// </summary>
    public int CallId { get; set; }

    /// <summary>
    /// Observed arrival time (if recorded).
    /// </summary>
    public TimeSpan? ObservedArrivalTime { get; set; }

    /// <summary>
    /// Observed departure time (if recorded).
    /// </summary>
    public TimeSpan? ObservedDepartureTime { get; set; }

    /// <summary>
    /// New track number (if changed from planned).
    /// </summary>
    public string? NewTrackNumber { get; set; }
}
