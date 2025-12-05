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
        
        if (isRestart)
        {
            var dispatchCalls = await _stateProvider.ReadDispatchCallsAsync(cancellationToken).ConfigureAwait(false);
            _trainSections = dispatchCalls.ToDictionary(d => d.Id);
        }
        else
        {
            var trackStretches = await _configuration.GetTrackStretchesAsync(cancellationToken).ConfigureAwait(false);
            var calls = await _configuration.GetTrainStationCallsAsync(cancellationToken).ConfigureAwait(false);
            _trainSections = calls.ToDispatchSections(trackStretches, TimeProvider, _logger).ToDictionary(c => c.Id);
            var result = await Persist();
        }
    }
}