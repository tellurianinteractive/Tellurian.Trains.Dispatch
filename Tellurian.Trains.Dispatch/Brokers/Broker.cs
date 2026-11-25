using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Brokers;

/// <summary>
/// Central broker for train dispatch operations.
/// </summary>
/// <param name="configuration">Data provider for layout and train configuration.</param>
/// <param name="compositeStateProvider">State provider for event-sourced state persistence.</param>
/// <param name="timeProvider">Time provider for scheduling.</param>
/// <param name="logger">Logger instance.</param>
public class Broker(
    IBrokerDataProvider configuration,
    ICompositeStateProvider compositeStateProvider,
    ITimeProvider timeProvider,
    ILogger<Broker> logger) : IBroker
{
    private readonly IBrokerDataProvider _configuration = configuration;
    private readonly ICompositeStateProvider _compositeStateProvider = compositeStateProvider;
    private readonly ILogger<Broker> _logger = logger;
    private readonly ActionStateMachine _actionStateMachine = new();

    private Dictionary<int, DispatchStretch> _dispatchStretches = [];
    private Dictionary<int, TrainSection> _trainSections = [];
    private Dictionary<int, StationDispatcher> _dispatchers = [];

    public ITimeProvider TimeProvider { get; } = timeProvider;

    /// <inheritdoc/>
    public TimeSpan ScheduleStartTime { get; private set; }

    /// <summary>
    /// Gets the composite state provider for recording state changes.
    /// </summary>
    public ICompositeStateProvider StateProvider => _compositeStateProvider;

    public IEnumerable<TrainSection> TrainSections => _trainSections.Values;

    public IEnumerable<ActionContext> GetArrivalActionsFor(IDispatcher dispatcher, int maxItems) =>
        _trainSections.Values
        .Where(ts => ts.IsVisibleForDispatcher && ts.ArrivalDispatcher?.Id == dispatcher.Id)
        .OrderBy(ts => ts.Arrival.Scheduled.ArrivalTime.InOperatingDayStartingAt(ScheduleStartTime))
        .ThenBy(ts => ts.Id)
        .Take(maxItems)
        .SelectMany(ts => _actionStateMachine.GetAvailableActions(ts, dispatcher));

    public IEnumerable<ActionContext> GetDepartureActionsFor(IDispatcher dispatcher, int maxItems) =>
        _trainSections.Values
        .Where(ts => ts.IsVisibleForDispatcher && ts.DepartureDispatcher?.Id == dispatcher.Id)
        .OrderBy(ts => ts.Departure.Scheduled.DepartureTime.InOperatingDayStartingAt(ScheduleStartTime))
        .ThenBy(ts => ts.Id)
        .Take(maxItems)
        .SelectMany(ts => _actionStateMachine.GetAvailableActions(ts, dispatcher));

    public IEnumerable<IDispatcher> GetDispatchers() => _dispatchers.Values;

    public async Task InitAsync(bool isRestart, CancellationToken cancellationToken = default)
    {
        var op = await _configuration.GetOperationPlacesAsync(cancellationToken).ConfigureAwait(false);
        var operationPlaces = op.ToDictionary(p => p.Id);
        _dispatchers = operationPlaces.Values.OfType<Station>().Select(s => new StationDispatcher(s, this)).ToDictionary(d => d.Id);
        foreach (var dispatcher in _dispatchers.Values)
        {
            dispatcher.Station.Dispatcher = dispatcher;
        }
        ResolveSignalControlledPlaceControlledBy(_dispatchers, operationPlaces);
        var ts = await _configuration.GetTrackStretchesAsync(cancellationToken).ConfigureAwait(false);
        var trackStretches = ts.ToDictionary(ts => ts.Id);
        var ds = await _configuration.GetDispatchStretchesAsync(cancellationToken).ConfigureAwait(false);
        _dispatchStretches = ds.ToDictionary(ds => ds.Id);

        // Always load initial configuration from data provider
        var trains = await _configuration.GetTrainsAsync(cancellationToken).ConfigureAwait(false);
        var calls = await _configuration.GetTrainStationCallsAsync(cancellationToken).ConfigureAwait(false);
        ScheduleStartTime = await _configuration.GetScheduleStartTimeAsync(cancellationToken).ConfigureAwait(false);
        var definitions = (await _configuration.GetTrainSectionsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var trainSections = definitions.Count > 0
            ? definitions.ToTrainSections(TimeProvider, _logger)
            : calls.ToTrainSections(_dispatchStretches.Values, TimeProvider, _logger);
        _trainSections = WithUpdatedPrevious(trainSections).ToDictionary(d => d.Id);

        // On restart, apply saved state from composite state provider
        if (isRestart && _compositeStateProvider.HasAnySavedState)
        {
            // Build dictionaries for trains and calls once
            var trainsById = _trainSections.Values
                .Select(s => s.Departure.Train)
                .DistinctBy(t => t.Id)
                .ToDictionary(t => t.Id);
            var callsById = _trainSections.Values
                .SelectMany(s => new[] { s.Departure, s.Arrival })
                .DistinctBy(c => c.Id)
                .ToDictionary(c => c.Id);

            await _compositeStateProvider.ApplyAllStateAsync(
                _trainSections,
                trainsById,
                callsById,
                operationPlaces,
                trackStretches,
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Restored state from composite state provider");
        }

        static void ResolveSignalControlledPlaceControlledBy(Dictionary<int, StationDispatcher> _dispatchers, Dictionary<int, OperationPlace> operationPlaces)
        {
            foreach (var scp in operationPlaces.Values.OfType<SignalControlledPlace>())
            {
                if (_dispatchers.TryGetValue(scp.ControlledByDispatcherId, out var controller))
                {
                    scp.ControlledBy = controller;
                }
            }
        }

        // This function eliminates the need to serialize the Previous propery and cause deep dependency graphs.
        static IEnumerable<TrainSection> WithUpdatedPrevious(IEnumerable<TrainSection> trainSections)
        {
            var sections = trainSections.ToList();
            foreach (var train in sections.GroupBy(ts => ts.Train.Id))
            {
                TrainSection? previous = null;
                foreach (var trainSection in train.OrderBy(ts => ts.Departure.SequenceNumber))
                {
                    trainSection.Previous = previous;
                    previous = trainSection;
                }
            }
            return sections;
        }
    }
}

