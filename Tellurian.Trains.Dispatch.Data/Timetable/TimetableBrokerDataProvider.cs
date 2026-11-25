using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Schedules.Model.Dispatching;
using Tellurian.Trains.Schedules.Model.Layouts;
using Tellurian.Trains.Schedules.Model.Schedules;
using Tellurian.Trains.Schedules.Model.Settings;
using Tellurian.Trains.Schedules.Model.Timetables;
using DispatchStation = Tellurian.Trains.Dispatch.Trains.Station;
using DispatchStretch = Tellurian.Trains.Dispatch.Layout.DispatchStretch;
using DispatchTrackStretch = Tellurian.Trains.Dispatch.Layout.TrackStretch;
using DispatchTrain = Tellurian.Trains.Dispatch.Trains.Train;
using ModelStation = Tellurian.Trains.Schedules.Model.Layouts.Station;
using ModelStationCall = Tellurian.Trains.Schedules.Model.Timetables.StationCall;
using ModelTimetable = Tellurian.Trains.Schedules.Model.Timetables.Timetable;
using ModelTrain = Tellurian.Trains.Schedules.Model.Timetables.Train;

namespace Tellurian.Trains.Dispatch.Data.Timetable;

/// <summary>
/// Implementation of <see cref="IBrokerDataProvider"/> that converts a plan of the timetable model
/// (<c>Tellurian.Trains.Schedules.Model</c>) to Dispatch domain types.
/// </summary>
/// <remarks>
/// <para>
/// The rules of the plan are the model's, and only taken from it here: who has a dispatcher, which sessions or days
/// each train runs on (<c>SessionNumbersOf</c>, including the first session's start), and who clears each part of a
/// train's run (<c>DispatchLegs</c>). What is added here is how dispatching works with them: each train is a train of
/// its own on every session or day it runs, with its own state, and a part of a run a dispatcher clears alone through a
/// junction they work from afar is presented together with the part beyond it.
/// </para>
/// <para>
/// Create it from a whole <see cref="Plan"/> so the driver duties are known; created from a timetable only, the first
/// session is decided by each train's own start. The plan is loaded lazily when the first data method is called.
/// </para>
/// </remarks>
public class TimetableBrokerDataProvider : IBrokerDataProvider
{
    private readonly Func<Task<Plan>> _planLoader;
    private Plan? _plan;

    private Dictionary<int, OperationPlace>? _placesByModelId;
    private List<DispatchTrackStretch>? _trackStretches;
    private List<DispatchStretch>? _dispatchStretches;
    private List<DispatchTrain>? _trains;
    private List<TrainStationCall>? _calls;
    private List<TrainSectionDefinition>? _sections;

    /// <summary>
    /// Creates a provider that loads a timetable when first needed.
    /// </summary>
    public TimetableBrokerDataProvider(Func<Task<ModelTimetable>> timetableLoader)
    {
        ArgumentNullException.ThrowIfNull(timetableLoader);
        _planLoader = async () => PlanOf(await timetableLoader().ConfigureAwait(false));
    }

    /// <summary>
    /// Creates a provider from a fully-loaded timetable.
    /// </summary>
    public TimetableBrokerDataProvider(ModelTimetable timetable)
    {
        ArgumentNullException.ThrowIfNull(timetable);
        _plan = PlanOf(timetable);
        _planLoader = () => Task.FromResult(_plan);
    }

    /// <summary>
    /// Creates a provider that loads a whole plan, including its driver duties, when first needed.
    /// </summary>
    public TimetableBrokerDataProvider(Func<Task<Plan>> planLoader)
    {
        _planLoader = planLoader ?? throw new ArgumentNullException(nameof(planLoader));
    }

    /// <summary>
    /// Creates a provider from a fully-loaded plan, including its driver duties.
    /// </summary>
    public TimetableBrokerDataProvider(Plan plan)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _planLoader = () => Task.FromResult(_plan);
    }

    public async Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _placesByModelId!.Values;
    }

    public async Task<IEnumerable<DispatchTrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _trackStretches!;
    }

    /// <summary>
    /// The plan's dispatch stretches that are between two stations with dispatchers. The train sections are given by
    /// <see cref="GetTrainSectionsAsync"/>, so these are for information only.
    /// </summary>
    public async Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _dispatchStretches!;
    }

    public async Task<IEnumerable<DispatchTrain>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _trains!;
    }

    public async Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _calls!;
    }

    /// <summary>
    /// The fast-clock time a session or day begins (the model's <c>SessionStart</c>).
    /// </summary>
    public async Task<TimeSpan> GetScheduleStartTimeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _plan!.Timetable.Layout.Settings.General.SessionStart;
    }

    public async Task<IEnumerable<TrainSectionDefinition>> GetTrainSectionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync().ConfigureAwait(false);
        return _sections!;
    }

    private static Plan PlanOf(ModelTimetable timetable) => new(timetable.Name, timetable);

    private async Task EnsureLoadedAsync()
    {
        _plan ??= await _planLoader().ConfigureAwait(false);
        if (_sections is not null) return;

        var layout = _plan.Timetable.Layout;
        _placesByModelId = layout.OperationLocations.ToDictionary(l => l.Id, l => l.ToOperationPlace());
        _trackStretches = [.. layout.TrackStretches.Select(s => s.ToDispatchTrackStretch(_placesByModelId)).OfType<DispatchTrackStretch>()];
        var stations = _placesByModelId.Values.OfType<DispatchStation>().ToDictionary(s => s.Id);
        _dispatchStretches = [.. layout.DispatchStretches.Select(s => s.ToDispatchStretch(stations, _trackStretches)).OfType<DispatchStretch>()];
        ConvertTrains();
    }

    /// <summary>
    /// Creates a train of its own for every session or day each train runs on, with its calls and sections.
    /// Session by session, in the timetable's order of trains, so that ids are the same each time the plan is loaded.
    /// </summary>
    private void ConvertTrains()
    {
        _trains = [];
        _calls = [];
        _sections = [];
        var plan = _plan!;
        var settings = plan.Timetable.Layout.Settings.General.SessionSettings();
        var sessionsByTrain = plan.Timetable.Trains.ToDictionary(t => t, t => plan.SessionNumbersOf(t));
        var legsByTrain = new Dictionary<ModelTrain, IReadOnlyList<DispatchLeg>>();

        foreach (var session in settings.PeriodNumbers)
        {
            foreach (var modelTrain in plan.Timetable.Trains.Where(t => sessionsByTrain[t].Contains(session)))
            {
                var legs = legsByTrain.TryGetValue(modelTrain, out var cached) ? cached : legsByTrain[modelTrain] = MergedLegs(modelTrain);
                if (legs.Count == 0) continue;
                var train = modelTrain.ToDispatchTrain() with { OperatingDay = session };
                _trains.Add(train);
                AddCallsAndSections(modelTrain, train, legs, TimeSpan.FromDays(session - 1));
            }
        }
    }

    /// <summary>
    /// Adds the calls the train is cleared at and a section for each leg. Times are given within the whole period of
    /// operation, <paramref name="offset"/> later for later sessions, so that the trains of all sessions are ordered after each other.
    /// </summary>
    private void AddCallsAndSections(ModelTrain modelTrain, DispatchTrain train, IReadOnlyList<DispatchLeg> legs, TimeSpan offset)
    {
        var points = legs.SelectMany(leg => new[] { leg.From, leg.To }).Distinct().ToList();
        var calls = new Dictionary<ModelStationCall, TrainStationCall>();
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].ToTrainStationCall(train, _placesByModelId!, i) is not { } call) continue;
            calls[points[i]] = call with
            {
                Scheduled = new CallTime { ArrivalTime = call.Scheduled.ArrivalTime + offset, DepartureTime = call.Scheduled.DepartureTime + offset },
                IsArrival = i > 0,
                IsDeparture = i < points.Count - 1
            };
        }
        _calls!.AddRange(calls.Values);

        var run = modelTrain.CallsInRunOrder;
        foreach (var leg in legs)
        {
            if (!calls.TryGetValue(leg.From, out var departure) || !calls.TryGetValue(leg.To, out var arrival)) continue;
            _sections!.Add(new TrainSectionDefinition(departure, arrival, KindOf(leg))
            {
                FromStation = StationOf(leg.FromDispatcher),
                ToStation = StationOf(leg.ToDispatcher),
                TrackStretches = TrackStretchesBetween(run, leg.From, leg.To)
            });
        }
    }

    /// <summary>
    /// How dispatchers act on a leg of the model.
    /// </summary>
    private static TrainSectionKind KindOf(DispatchLeg leg) => leg.Kind switch
    {
        DispatchLegKind.BetweenDispatchers => (leg.From.OperationLocation.Dispatcher, leg.To.OperationLocation.Dispatcher) switch
        {
            (not null, not null) => TrainSectionKind.Normal,
            (null, not null) => TrainSectionKind.Entering,
            _ => TrainSectionKind.Leaving
        },
        DispatchLegKind.DriverCalls => leg.FromDispatcher is not null ? TrainSectionKind.OneDispatcherOutbound : TrainSectionKind.OneDispatcherInbound,
        _ => leg.From.OperationLocation.HasDispatcher ? TrainSectionKind.OneDispatcherOutbound : TrainSectionKind.OneDispatcherInbound
    };

    /// <summary>
    /// The model's legs, where a leg a dispatcher clears alone up to a junction they work from afar is presented
    /// together with the leg beyond the junction that they also clear: the dispatcher at Gårdsjö asks Rotebro for a
    /// train Gårdsjö → Tjärby → Rotebro before sending it out, and a driver calling Steinsnes to run Triangeln →
    /// Steinsnes vest → Steinsnes is given the go all the way.
    /// </summary>
    private static List<DispatchLeg> MergedLegs(ModelTrain modelTrain)
    {
        var legs = modelTrain.DispatchLegs.ToList();
        var merged = new List<DispatchLeg>();
        for (var i = 0; i < legs.Count; i++)
        {
            var leg = legs[i];
            if (leg.Kind == DispatchLegKind.OneDispatcher)
            {
                if (!leg.To.OperationLocation.HasDispatcher && i + 1 < legs.Count && legs[i + 1].From == leg.To && Involves(legs[i + 1], leg.FromDispatcher))
                {
                    legs[i + 1] = Merge(leg, legs[i + 1]);
                    continue;
                }
                if (!leg.From.OperationLocation.HasDispatcher && merged.Count > 0 && merged[^1].To == leg.From && Involves(merged[^1], leg.FromDispatcher))
                {
                    merged[^1] = Merge(merged[^1], leg);
                    continue;
                }
            }
            merged.Add(leg);
        }
        return merged;

        static bool Involves(DispatchLeg leg, ModelStation? station) =>
            station is not null && (station.Equals(leg.FromDispatcher) || station.Equals(leg.ToDispatcher));

        static DispatchLeg Merge(DispatchLeg first, DispatchLeg second)
        {
            var other = first.Kind == DispatchLegKind.OneDispatcher ? second : first;
            return new DispatchLeg(first.From, second.To, other.Kind) { FromDispatcher = other.FromDispatcher, ToDispatcher = other.ToDispatcher };
        }
    }

    private DispatchStation? StationOf(ModelStation? station) =>
        station is not null && _placesByModelId!.TryGetValue(station.Id, out var place) ? place as DispatchStation : null;

    /// <summary>
    /// The track stretches the train runs on from <paramref name="from"/> to <paramref name="to"/>, in the order of
    /// travel; empty where a stretch between two consecutive places of the run is missing.
    /// </summary>
    private List<DispatchTrackStretch> TrackStretchesBetween(IReadOnlyList<ModelStationCall> run, ModelStationCall from, ModelStationCall to)
    {
        var start = IndexOf(run, from);
        var end = IndexOf(run, to);
        var result = new List<DispatchTrackStretch>();
        for (var i = start; i >= 0 && i < end; i++)
        {
            var a = run[i].OperationLocation.Id;
            var b = run[i + 1].OperationLocation.Id;
            var stretch = _trackStretches!.FirstOrDefault(s =>
                (s.Start.Id == a && s.End.Id == b) || (s.Start.Id == b && s.End.Id == a));
            if (stretch is null) return [];
            result.Add(stretch);
        }
        return result;

        static int IndexOf(IReadOnlyList<ModelStationCall> calls, ModelStationCall call)
        {
            for (var i = 0; i < calls.Count; i++) if (calls[i] == call) return i;
            return -1;
        }
    }
}
