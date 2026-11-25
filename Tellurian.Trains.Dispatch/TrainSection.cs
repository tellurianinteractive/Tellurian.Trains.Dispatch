using System.Text.Json.Serialization;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Represents a <see cref="Train">train's</see> movement that is cleared by one or two
/// <see cref="IDispatcher">dispatchers</see>; see <see cref="TrainSectionKind"/>.
/// </summary>
public class TrainSection
{
    [JsonInclude]
    public int Id { get; internal set { field = value.OrNextId; } }

    /// <summary>
    /// How this section is cleared, which decides which dispatcher does what.
    /// </summary>
    public TrainSectionKind Kind { get; }

    /// <summary>
    /// The direction of travel on the dispatch stretch, for a section created from one; otherwise null.
    /// </summary>
    public DispatchStretchDirection? StretchDirection { get; }

    [JsonIgnore]
    public DispatchStretch? DispatchStretch => StretchDirection?.DispatchStretch;

    /// <summary>
    /// The station whose dispatcher clears the train at the end it comes from: of the line in the direction of
    /// travel, also when the train starts at a place along it. Null where that end has no dispatcher.
    /// </summary>
    [JsonIgnore]
    public Station? FromStation { get; }

    /// <summary>
    /// The station whose dispatcher clears the train at the end it goes to: of the line in the direction of
    /// travel, also when the train ends at a place along it. Null where that end has no dispatcher.
    /// </summary>
    [JsonIgnore]
    public Station? ToStation { get; }

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
    /// The track stretches the train runs on in this section, in the order of travel.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<TrackStretch> TrackStretches { get; }

    /// <summary>
    /// The places the train passes in this section in the order of travel, from departure to arrival.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<OperationPlace> Places { get; }

    /// <summary>
    /// The current TrackStretch the train is on, or null if not yet departed.
    /// </summary>
    public TrackStretch? CurrentTrackStretch =>
        State == DispatchState.Departed && CurrentTrackStretchIndex < TrackStretches.Count
            ? TrackStretches[CurrentTrackStretchIndex]
            : null;

    /// <summary>
    /// The next control point the train will reach, or null if approaching destination.
    /// </summary>
    public OperationPlace? NextControlPoint =>
        CurrentTrackStretch is not null && CurrentTrackStretchIndex < TrackStretches.Count - 1
            ? Places[CurrentTrackStretchIndex + 1]
            : null;

    /// <summary>
    /// Signal-controlled places the train passes in this section that a dispatcher controls, in the order of travel.
    /// </summary>
    public IEnumerable<SignalControlledPlace> IntermediateControlPoints =>
        Places.Skip(1).Take(Math.Max(0, Places.Count - 2)).OfType<SignalControlledPlace>().Where(place => place.ControlledBy is not null);

    /// <summary>
    /// Total number of TrackStretches to traverse.
    /// </summary>
    public int TotalTrackStretches => TrackStretches.Count;

    /// <summary>
    /// True when train has passed all intermediate control points and is on the last TrackStretch.
    /// </summary>
    public bool IsOnLastTrackStretch => CurrentTrackStretchIndex >= TotalTrackStretches - 1;

    /// <summary>
    /// The dispatcher who has this section among its departures.
    /// </summary>
    public IDispatcher? DepartureDispatcher => Kind == TrainSectionKind.OneDispatcherInbound ? null : FromStation?.Dispatcher;

    /// <summary>
    /// The dispatcher who has this section among its arrivals.
    /// </summary>
    public IDispatcher? ArrivalDispatcher => Kind == TrainSectionKind.OneDispatcherOutbound ? null : ToStation?.Dispatcher;

    /// <summary>
    /// The dispatcher who requests (and may revoke) clearance for the train.
    /// </summary>
    public IDispatcher? RequestingDispatcher => Kind switch
    {
        TrainSectionKind.Normal or TrainSectionKind.Leaving => FromStation?.Dispatcher,
        TrainSectionKind.Entering => ToStation?.Dispatcher,
        _ => null
    };

    /// <summary>
    /// The dispatcher who accepts or rejects clearance for the train.
    /// </summary>
    public IDispatcher? AcceptingDispatcher => Kind switch
    {
        TrainSectionKind.Normal or TrainSectionKind.Leaving or TrainSectionKind.OneDispatcherInbound => ToStation?.Dispatcher,
        TrainSectionKind.Entering => FromStation?.Dispatcher,
        _ => null
    };

    /// <summary>
    /// The dispatcher who marks the train as departed.
    /// </summary>
    public IDispatcher? DepartingDispatcher => Kind switch
    {
        TrainSectionKind.Normal or TrainSectionKind.Leaving or TrainSectionKind.OneDispatcherOutbound => FromStation?.Dispatcher,
        TrainSectionKind.Entering => ToStation?.Dispatcher,
        _ => null // OneDispatcherInbound: accepting is the go.
    };

    /// <summary>
    /// The dispatcher who marks the train as arrived.
    /// </summary>
    public IDispatcher? ArrivingDispatcher => Kind switch
    {
        TrainSectionKind.Normal or TrainSectionKind.Entering or TrainSectionKind.OneDispatcherInbound => ToStation?.Dispatcher,
        TrainSectionKind.Leaving => FromStation?.Dispatcher,
        _ => null // OneDispatcherOutbound: done when departed.
    };

    /// <summary>
    /// All dispatchers involved in this section.
    /// </summary>
    public IEnumerable<IDispatcher> Dispatchers =>
        new[] { DepartureDispatcher, ArrivalDispatcher, RequestingDispatcher, AcceptingDispatcher, DepartingDispatcher, ArrivingDispatcher }
        .OfType<IDispatcher>()
        .DistinctBy(d => d.Id);

    public override int GetHashCode() => Id.GetHashCode();

    public override bool Equals(object? obj) => obj is TrainSection section && section.Id == Id;

    public override string ToString() => $"{this.Train.Identity} {this.From.Signature}→{this.To.Signature}";

    [JsonIgnore]
    public ITimeProvider TimeProvider { get; private set; }

    private TrainSection(
        TrainSectionKind kind,
        DispatchStretchDirection? direction,
        Station? fromStation,
        Station? toStation,
        IReadOnlyList<TrackStretch> trackStretches,
        TrainStationCall departure,
        TrainStationCall arrival,
        ITimeProvider timeProvider)
    {
        Id = 0; // Trigger auto-increment
        Kind = kind;
        StretchDirection = direction;
        FromStation = fromStation;
        ToStation = toStation;
        TrackStretches = trackStretches;
        Places = trackStretches.PlacesFrom(departure.At, arrival.At);
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
        Kind = TrainSectionKind.Normal;
        StretchDirection = stretchDirection;
        FromStation = stretchDirection.From;
        ToStation = stretchDirection.To;
        TrackStretches = [.. stretchDirection.TrackStretchesInOrder];
        Places = TrackStretches.PlacesFrom(departure.At, arrival.At);
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
    /// Creates a <see cref="TrainSectionKind.Normal"/> <see cref="TrainSection"/> between the stations of a dispatch stretch.
    /// </summary>
    public static Option<TrainSection> Create(DispatchStretch dispatchStretch, TrainStationCall departure, TrainStationCall arrival, ITimeProvider timeProvider)
    {
        foreach (var direction in new[] { dispatchStretch.Forward, dispatchStretch.Reverse })
        {
            if (direction.CanHave(departure, arrival))
            {
                return Option<TrainSection>.Success(new TrainSection(
                    TrainSectionKind.Normal, direction, direction.From, direction.To, [.. direction.TrackStretchesInOrder], departure, arrival, timeProvider));
            }
        }
        return Option<TrainSection>.Fail("Departure or arrival not on track stretch or they are not on the same train.");
    }

    /// <summary>
    /// Creates a <see cref="TrainSection"/> from what a data provider says about it.
    /// </summary>
    public static Option<TrainSection> Create(TrainSectionDefinition definition, ITimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var hasRequiredStations = definition.Kind switch
        {
            TrainSectionKind.OneDispatcherOutbound => definition.FromStation is not null,
            TrainSectionKind.OneDispatcherInbound => definition.ToStation is not null,
            _ => definition.FromStation is not null && definition.ToStation is not null
        };
        if (!hasRequiredStations)
            return Option<TrainSection>.Fail($"A {definition.Kind} section {definition.Departure.At}→{definition.Arrival.At} lacks the station of a dispatcher.");
        if (definition.Departure.Train.Id != definition.Arrival.Train.Id)
            return Option<TrainSection>.Fail("Departure and arrival are not on the same train.");
        return Option<TrainSection>.Success(new TrainSection(
            definition.Kind, null, definition.FromStation, definition.ToStation, definition.TrackStretches,
            definition.Departure, definition.Arrival, timeProvider));
    }

    /// <summary>
    /// Advances the train to the next TrackStretch after passing a control point.
    /// </summary>
    internal bool AdvanceToNextTrackStretch()
    {
        if (State != DispatchState.Departed) return false;
        if (IsOnLastTrackStretch) return false;
        CurrentTrackStretchIndex++;
        AdvancePastUncontrolledPlaces();
        return true;
    }

    /// <summary>
    /// Moves the train past places no dispatcher has to mark, up to the next signal a dispatcher controls:
    /// places without signals, such as industrial areas and unmanned stations, and signals the driver works.
    /// Only signals a dispatcher controls divide the section into blocks.
    /// </summary>
    internal void AdvancePastUncontrolledPlaces()
    {
        while (State == DispatchState.Departed && !IsOnLastTrackStretch &&
            NextControlPoint is not SignalControlledPlace { ControlledBy: not null })
        {
            CurrentTrackStretchIndex++;
        }
    }
}

internal static class TrackStretchPlaces
{
    extension(IReadOnlyList<TrackStretch> trackStretches)
    {
        /// <summary>
        /// The places along the track stretches in the order of travel, starting at <paramref name="start"/>.
        /// Without track stretches, the places are <paramref name="start"/> and <paramref name="end"/>.
        /// </summary>
        internal List<OperationPlace> PlacesFrom(OperationPlace start, OperationPlace end)
        {
            if (trackStretches.Count == 0) return [start, end];
            var places = new List<OperationPlace> { start };
            var current = start;
            foreach (var stretch in trackStretches)
            {
                current = stretch.Start.Equals(current) ? stretch.End : stretch.Start;
                places.Add(current);
            }
            return places;
        }
    }
}
