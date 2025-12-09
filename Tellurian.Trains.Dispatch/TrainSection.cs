using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Represents a <see cref="Train">train's</see> movement between adjacent <see cref="Station">stations</see> that
/// is controlled by two <see cref="IDispatcher">dispatchers</see>.
/// </summary>
public class TrainSection
{
    [JsonInclude]
    public int Id { get; internal set { field = value.OrNextId; } }
    public DispatchStretchDirection StretchDirection { get; }
    [JsonIgnore]
    public DispatchStretch DispatchStretch => StretchDirection.DispatchStretch;
    public TrainStationCall Departure { get; }
    public TrainStationCall Arrival { get; }
    [JsonInclude]
    public DispatchState State { get; internal set; }

    /// <summary>
    /// The previous <see cref="TrainSection"/> for the same train.
    /// Null for the first section of a train's journey.
    /// </summary>
    [JsonIgnore]
    public TrainSection? Previous { get; internal set; }

    /// <summary>
    /// Index of the current TrackStretch the train is on.
    /// Starts at 0 when departed, increments as train passes each control point.
    /// </summary>
    [JsonInclude]
    public int CurrentTrackStretchIndex { get; internal set; }

    /// <summary>
    /// The current TrackStretch the train is on, or null if not yet departed.
    /// </summary>
    public TrackStretch? CurrentTrackStretch =>
        State == DispatchState.Departed && CurrentTrackStretchIndex < DispatchStretch.TrackStretches.Count
            ? StretchDirection.TrackStretchesInOrder.ElementAt(CurrentTrackStretchIndex)
            : null;

    /// <summary>
    /// The next control point the train will reach, or null if approaching destination.
    /// </summary>
    public OperationPlace? NextControlPoint =>
        CurrentTrackStretchIndex < DispatchStretch.TrackStretches.Count - 1
            ? CurrentTrackStretch?.End
            : null;

    /// <summary>
    /// Total number of TrackStretches to traverse.
    /// </summary>
    public int TotalTrackStretches => DispatchStretch.TrackStretches.Count;

    /// <summary>
    /// True when train has passed all intermediate control points and is on the last TrackStretch.
    /// </summary>
    public bool IsOnLastTrackStretch => CurrentTrackStretchIndex >= TotalTrackStretches - 1;

    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is TrainSection section && section.Id == Id;
    public override string ToString() => $"{this.Train.Identity} {this.From.Signature}→{this.To.Signature}";

    [JsonIgnore]
    public ITimeProvider TimeProvider { get; private set; }

    private TrainSection(DispatchStretchDirection direction, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        Id = 0; // Trigger auto-increment
        StretchDirection = direction;
        Departure = departure;
        Arrival = arrival;
        TimeProvider = timeProvider;
    }

    /// <summary>
    /// Constructor for JSON deserialization.
    /// </summary>
    [JsonConstructor]
    internal TrainSection(int id, DispatchStretchDirection stretchDirection, TrainStationCall departure, TrainStationCall arrival, DispatchState state, int currentTrackStretchIndex)
    {
        Id = id;
        StretchDirection = stretchDirection;
        Departure = departure;
        Arrival = arrival;
        State = state;
        CurrentTrackStretchIndex = currentTrackStretchIndex;
        TimeProvider = DefaultTimeProvider.Instance;
    }

    /// <summary>
    /// Sets the time provider after JSON deserialization.
    /// </summary>
    public void SetTimeProvider(ITimeProvider timeProvider) => TimeProvider = timeProvider;

    /// <summary>
    /// Applies saved state from persistence.
    /// </summary>
    public void ApplyState(DispatchState state, int currentTrackStretchIndex)
    {
        State = state;
        CurrentTrackStretchIndex = currentTrackStretchIndex;
    }

    /// <summary>
    /// Creates a <see cref="TrainSection"/>.
    /// </summary>
    public static Option<TrainSection> Create(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        if (dispatchStretch.Forward.CanHave(departure, arrival))
        {
            var trainSection = new TrainSection(dispatchStretch.Forward, departure, arrival, timeProvider);
            return Option<TrainSection>.Success(trainSection);
        }
        else if (dispatchStretch.Reverse.CanHave(departure, arrival))
        {
            var trainSection = new TrainSection(dispatchStretch.Reverse, departure, arrival, timeProvider);
            return Option<TrainSection>.Success(trainSection);
        }
        else
        {
            return Option<TrainSection>.Fail("Departure or arrival not on track stretch or they are not on the same train.");
        }
    }

    /// <summary>
    /// Advances the train to the next TrackStretch after passing a control point.
    /// </summary>
    internal bool AdvanceToNextTrackStretch()
    {
        if (State != DispatchState.Departed) return false;
        if (IsOnLastTrackStretch) return false;

        CurrentTrackStretchIndex++;
        return true;
    }
}
