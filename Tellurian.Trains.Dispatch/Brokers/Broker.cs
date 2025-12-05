using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Configurations;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;

public class Broker(IBrokerConfiguration configuration, IBrokerStateProvider stateProvider, ITimeProvider timeProvider, ILogger<Broker> logger): IBroker
{
    private readonly IBrokerConfiguration _configuration = configuration;
    private readonly IBrokerStateProvider _stateProvider = stateProvider;
    private readonly ILogger<Broker> _logger = logger;

    private Dictionary<int, TrainSection> _trainSections = [];
    private Dictionary<int, StationDispatcher> _dispatchers = [];
    private Task<bool> Persist() => _stateProvider.SaveDispatchCallsAsync(_trainSections.Values);

    public ITimeProvider TimeProvider { get; } = timeProvider;

    public IEnumerable<TrainSection> GetArrivalsFor(Station? station, int maxItems) =>
            _trainSections.Values
            .Where(dt => dt.IsVisibleForDispatcher && (station is null || dt.To.Equals(station)))
            .Take(maxItems);

    public IEnumerable<TrainSection> GetDeparturesFor(Station? station, int maxItems) =>
        _trainSections.Values
        .Where(dt => dt.IsVisibleForDispatcher && (station is null || dt.From.Equals(station)))
        .Take(maxItems);

    public IEnumerable<IDispatcher> GetDispatchers() => _dispatchers.Values;

    public async Task InitAsync(bool isRestart, CancellationToken cancellationToken = default)
    {
        var stations = await _configuration.GetStationsAsync(cancellationToken).ConfigureAwait(false);
        _dispatchers = stations.Select(s => new StationDispatcher(s, this)).ToDictionary(d => d.Id);
        foreach (var station in stations) {
            station.Dispatcher = _dispatchers[station.Id];
        }

        var trackStretches = await _configuration.GetTrackStretchesAsync(cancellationToken).ConfigureAwait(false);
        ResolveBlockSignalControllers(trackStretches);

        if (isRestart)
        {
            var dispatchCalls = await _stateProvider.ReadDispatchCallsAsync(cancellationToken).ConfigureAwait(false);
            _trainSections = dispatchCalls.ToDictionary(d => d.Id);
        }
        else
        {
            var calls = await _configuration.GetTrainStationCallsAsync(cancellationToken).ConfigureAwait(false);
            _trainSections = calls.ToDispatchSections(trackStretches, TimeProvider, _logger).ToDictionary(c => c.Id);
            var result = await Persist();
        }
    }

    /// <summary>
    /// Resolves placeholder BlockSignal.ControlledBy references to actual StationDispatcher instances.
    /// </summary>
    private void ResolveBlockSignalControllers(IEnumerable<Layout.DispatchStretch> trackStretches)
    {
        var dispatchersByName = _dispatchers.Values.ToDictionary(d => d.Name);
        foreach (var stretch in trackStretches)
        {
            ResolveBlockSignals(stretch.Forward.IntermediateBlockSignals, dispatchersByName);
            ResolveBlockSignals(stretch.Reverse.IntermediateBlockSignals, dispatchersByName);
        }
    }

    private static void ResolveBlockSignals(IList<Layout.BlockSignal> blockSignals, Dictionary<string, StationDispatcher> dispatchersByName)
    {
        foreach (var blockSignal in blockSignals)
        {
            if (dispatchersByName.TryGetValue(blockSignal.ControlledBy.Name, out var dispatcher))
            {
                blockSignal.ControlledBy = dispatcher;
            }
        }
    }
}