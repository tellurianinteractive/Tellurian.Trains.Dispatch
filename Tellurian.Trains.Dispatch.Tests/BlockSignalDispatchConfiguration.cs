using Tellurian.Trains.Dispatch.Configurations;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Configuration with block signals between stations.
/// </summary>
internal class BlockSignalDispatchConfiguration : IBrokerConfiguration
{
    // Stations
    private readonly Station _stationA;
    private readonly Station _stationB;

    // Train
    private readonly Company _company;
    private readonly Train _train;

    // Stretch with one block signal
    private readonly DispatchStretch _stretch;
    private readonly BlockSignal _blockSignal;

    // Calls
    private readonly List<TrainStationCall> _calls;

    public BlockSignalDispatchConfiguration()
    {
        // Initialize entities and trigger ID generation by setting Id = 0
        _stationA = new("Alpha", "A") { Id = 0 };
        _stationB = new("Beta", "B") { Id = 0 };
        _company = new("Test Railway", "TR");
        _train = new Train(_company, new Identity("IC", 201)) { Id = 0 };

        // Block signal controlled by station B dispatcher (name must match station name)
        var controllerRef = new BlockSignalControllerDispatcher("Beta");
        _blockSignal = new BlockSignal("Signal 1", controllerRef) { Id = 0 };

        _stretch = new DispatchStretch(_stationA, _stationB, numberOfTracks: 1,
            intermediateBlockSignals: [_blockSignal]);

        // Create station calls
        // Note: DepartureTime must be set on all calls for proper ordering in ToDispatchSections
        var departureCall = new TrainStationCall(_train, _stationA,
            new CallTime { DepartureTime = TimeSpan.FromHours(11) })
        {
            Id = 0,
            Track = new StationTrack("1")
        };

        var arrivalCall = new TrainStationCall(_train, _stationB,
            new CallTime { DepartureTime = TimeSpan.FromHours(11.5), ArrivalTime = TimeSpan.FromHours(11.5) })
        {
            Id = 0,
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
