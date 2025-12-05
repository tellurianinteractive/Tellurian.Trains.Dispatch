using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Configurations;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// In-memory test configuration for a simple two-station scenario with one train.
/// </summary>
internal class SimpleDispatchConfiguration : IBrokerConfiguration
{
    // Stations
    private readonly Station _stationA = new("Alpha", "A");
    private readonly Station _stationB = new("Beta", "B");

    // Train
    private readonly Company _company = new("Test Railway", "TR");
    private readonly Train _train;

    // Stretch
    private readonly DispatchStretch _stretch;

    // Calls
    private readonly List<TrainStationCall> _calls;

    public SimpleDispatchConfiguration()
    {
        _train = new Train(_company, new Identity("IC", 101));
        _stretch = new DispatchStretch(_stationA, _stationB);

        // Create station calls: departure from A at 10:00, arrival at B at 10:30
        var departureCall = new TrainStationCall(_train, _stationA,
            new CallTime { DepartureTime = TimeSpan.FromHours(10) })
        {
            IsArrival = false,
            IsDeparture = true,
            Track = new StationTrack("1")
        };

        var arrivalCall = new TrainStationCall(_train, _stationB,
            new CallTime { ArrivalTime = TimeSpan.FromHours(10.5) })
        {
            IsArrival = true,
            IsDeparture = false,
            Track = new StationTrack("2")
        };

        _calls = [departureCall, arrivalCall];
    }

    public Task<IEnumerable<Station>> GetStationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Station>>([_stationA, _stationB]);

    public Task<IEnumerable<DispatchStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<DispatchStretch>>([_stretch]);

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TrainStationCall>>(_calls);
}

/// <summary>
/// Configuration with block signals between stations.
/// </summary>
internal class BlockSignalDispatchConfiguration : IBrokerConfiguration
{
    // Stations
    private readonly Station _stationA = new("Alpha", "A");
    private readonly Station _stationB = new("Beta", "B");

    // Train
    private readonly Company _company = new("Test Railway", "TR");
    private readonly Train _train;

    // Stretch with one block signal
    private readonly DispatchStretch _stretch;
    private readonly BlockSignal _blockSignal;

    // Calls
    private readonly List<TrainStationCall> _calls;

    public BlockSignalDispatchConfiguration()
    {
        _train = new Train(_company, new Identity("IC", 201));

        // Block signal controlled by station B dispatcher (name must match station name)
        var controllerRef = new BlockSignalControllerReference("Beta");
        _blockSignal = new BlockSignal("Signal 1", controllerRef);

        _stretch = new DispatchStretch(_stationA, _stationB, numberOfTracks: 1,
            intermediateBlockSignals: [_blockSignal]);

        // Create station calls
        var departureCall = new TrainStationCall(_train, _stationA,
            new CallTime { DepartureTime = TimeSpan.FromHours(11) })
        {
            IsArrival = false,
            IsDeparture = true,
            Track = new StationTrack("1")
        };

        var arrivalCall = new TrainStationCall(_train, _stationB,
            new CallTime { ArrivalTime = TimeSpan.FromHours(11.5) })
        {
            IsArrival = true,
            IsDeparture = false,
            Track = new StationTrack("2")
        };

        _calls = [departureCall, arrivalCall];
    }

    public Task<IEnumerable<Station>> GetStationsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<Station>>([_stationA, _stationB]);

    public Task<IEnumerable<DispatchStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<DispatchStretch>>([_stretch]);

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<TrainStationCall>>(_calls);
}

/// <summary>
/// Reference to a dispatcher by name for BlockSignal.ControlledBy configuration.
/// Only the Name is used - matching is done by name against actual StationDispatcher instances.
/// </summary>
/// <remarks>
/// This is needed because BlockSignal is configured before the Broker creates StationDispatcher instances.
/// The actual dispatching uses StationDispatcher from Broker.GetDispatchers().
/// </remarks>
internal class BlockSignalControllerReference(string name) : IDispatcher
{
    public string Name { get; } = name;
    public IEnumerable<TrainSection> Departures => throw new NotSupportedException("Use StationDispatcher from Broker");
    public IEnumerable<TrainSection> Arrivals => throw new NotSupportedException("Use StationDispatcher from Broker");
}

/// <summary>
/// Thread-safe in-memory state provider for tests.
/// </summary>
/// <remarks>
/// Only one operation at a time. If Save is already ongoing, returns immediately without saving.
/// </remarks>
internal class InMemoryStateProvider : IBrokerStateProvider
{
    private readonly List<TrainSection> _savedStretches = [];
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IEnumerable<TrainSection>> ReadDispatchCallsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return [.. _savedStretches];
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task<bool> SaveDispatchCallsAsync(IEnumerable<TrainSection> dispatchCalls, CancellationToken cancellationToken = default)
    {
        if (!_semaphore.Wait(0, cancellationToken))
            return Task.FromResult(false);

        try
        {
            _savedStretches.Clear();
            _savedStretches.AddRange(dispatchCalls);
            return Task.FromResult(true);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Simple time provider that returns scheduled time or current time.
/// </summary>
internal class TestTimeProvider : ITimeProvider
{
    public TimeSpan CurrentTime { get; set; } = TimeSpan.FromHours(10);

    public TimeSpan Time(TimeSpan? scheduledTime = null) =>
        scheduledTime ?? CurrentTime;
}
