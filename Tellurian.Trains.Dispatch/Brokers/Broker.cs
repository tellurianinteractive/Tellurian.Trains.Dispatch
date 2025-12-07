using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Brokers;

public class Broker(IBrokerDataProvider configuration, IBrokerStateProvider stateProvider, ITimeProvider timeProvider, ILogger<Broker> logger) : IBroker
{
    private readonly IBrokerDataProvider _configuration = configuration;
    private readonly IBrokerStateProvider _stateProvider = stateProvider;
    private readonly ILogger<Broker> _logger = logger;
    private readonly ActionStateMachine _actionStateMachine = new();

    private Dictionary<int, DispatchStretch> _dispatchStretches = [];
    private Dictionary<int, TrainSection> _trainSections = [];
    private Dictionary<int, StationDispatcher> _dispatchers = [];
    private Task<bool> Persist() => _stateProvider.SaveDispatchCallsAsync(_trainSections.Values);

    public ITimeProvider TimeProvider { get; } = timeProvider;

    public IEnumerable<ActionContext> GetArrivalActionsFor(IDispatcher dispatcher, int maxItems) =>
        _trainSections.Values
        .Where(ts => ts.IsVisibleForDispatcher && ts.To.Dispatcher.Id == dispatcher.Id)
        .Take(maxItems)
        .SelectMany(ts => _actionStateMachine.GetAvailableActions(ts, dispatcher));

    public IEnumerable<ActionContext> GetDepartureActionsFor(IDispatcher dispatcher, int maxItems) =>
        _trainSections.Values
        .Where(ts => ts.IsVisibleForDispatcher && ts.From.Dispatcher.Id == dispatcher.Id)
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
        var ts = await _configuration.GetTrackStretchesAsync(cancellationToken).ConfigureAwait(false);
        var trackStretches = ts.ToDictionary(ts => ts.Id);
        var ds = await _configuration.GetDispatchStretchesAsync(cancellationToken).ConfigureAwait(false);
        _dispatchStretches = ds.ToDictionary(ds => ds.Id);

        if (isRestart)
        {
            var states = await _stateProvider.ReadTrainSections(cancellationToken).ConfigureAwait(false);
            _trainSections = states.ToDictionary(d => d.Id);
        }
        else
        {
            var trains = await _configuration.GetTrainsAsync(cancellationToken).ConfigureAwait(false);
            var calls = await _configuration.GetTrainStationCallsAsync(cancellationToken).ConfigureAwait(false);
            _trainSections = calls.ToTrainSections(_dispatchStretches.Values, TimeProvider, _logger).ToDictionary(c => c.Id);
            var result = await Persist();
        }
    }
}

