using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// In-memory test configuration for a simple two-station scenario with one train.
/// </summary>
internal class SimpleDispatchConfiguration : IBrokerDataProvider
{
    private readonly Station _stationA;
    private readonly Station _stationB;
    private readonly IEnumerable<OperationPlace> _operationPlaces;
    private readonly IEnumerable<TrackStretch> _trackStretches;
    private readonly Company _company;
    private readonly Train _train;
    private readonly DispatchStretch _dispatchStretch;

    // Calls
    private readonly List<TrainStationCall> _calls;

    public SimpleDispatchConfiguration()
    {
        // Initialize entities and trigger ID generation by setting Id = 0
        _stationA = new("Alpha", "A") { Id = 0 };
        _stationB = new("Beta", "B") { Id = 0 };
        _operationPlaces = [_stationB, _stationB];
        _company = new("Test Railway", "TR");
        _train = new Train(_company, new Identity("IC", 101)) { Id = 0, };
        _trackStretches = []; // TODO: Implement testdata.

        _dispatchStretch = new DispatchStretch(_stationA, _stationB, _trackStretches);

        // Create station calls: departure from A at 10:00, arrival at B at 10:30
        // Note: DepartureTime must be set on all calls for proper ordering in ToDispatchSections
        var departureCall = new TrainStationCall(_train, _stationA,
            new CallTime { DepartureTime = TimeSpan.FromHours(10) })
        {
            Id = 0,
            PlannedTrack = new StationTrack("1")
        };

        var arrivalCall = new TrainStationCall(_train, _stationB,
            new CallTime { DepartureTime = TimeSpan.FromHours(10.5), ArrivalTime = TimeSpan.FromHours(10.5) })
        {
            Id = 0,
            PlannedTrack = new StationTrack("2")
        };

        _calls = [departureCall, arrivalCall];
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_operationPlaces);
    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_trackStretches);
    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new DispatchStretch[] { _dispatchStretch }.AsEnumerable());
    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new Train[] { _train }.AsEnumerable());
    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_calls.AsEnumerable());
}
