using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Tests;

/// <summary>
/// Simple test data provider for CSV state provider tests.
/// Creates a minimal layout with two stations and one train.
/// Uses fixed IDs so state can be restored consistently across broker restarts.
/// </summary>
internal sealed class CsvTestDataProvider : IBrokerDataProvider
{
    // Fixed IDs - must be positive to prevent auto-generation
    private const int StationAId = 1001;
    private const int StationBId = 1002;
    private const int TrackStretchId = 2001;
    private const int DispatchStretchId = 3001;
    private const int TrainId = 4001;
    private const int CallAId = 5001;
    private const int CallBId = 5002;

    private readonly Station _stationA;
    private readonly Station _stationB;
    private readonly TrackStretch _stretch;
    private readonly Company _testCompany = new("Test Railway", "TR");
    private Train? _train;

    public CsvTestDataProvider()
    {
        _stationA = new("Station A", "A")
        {
            Id = StationAId,
            Tracks = [new StationTrack("1"), new StationTrack("2")]
        };

        _stationB = new("Station B", "B")
        {
            Id = StationBId,
            Tracks = [new StationTrack("1")]
        };

        _stretch = new TrackStretch(_stationA, _stationB) { Id = TrackStretchId };
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<OperationPlace> places = [_stationA, _stationB];
        return Task.FromResult(places);
    }

    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<TrackStretch> stretches = [_stretch];
        return Task.FromResult(stretches);
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        var dispatch = new DispatchStretch(_stationA, _stationB, [_stretch]) { Id = DispatchStretchId };
        IEnumerable<DispatchStretch> dispatches = [dispatch];
        return Task.FromResult(dispatches);
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        _train = new Train(_testCompany, new Identity("P", 101)) { Id = TrainId };
        IEnumerable<Train> trains = [_train];
        return Task.FromResult(trains);
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        if (_train is null)
            throw new InvalidOperationException("GetTrainsAsync must be called before GetTrainStationCallsAsync");

        var calls = new List<TrainStationCall>
        {
            new(_train, _stationA, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10),
                DepartureTime = TimeSpan.FromHours(10)
            })
            {
                Id = CallAId,
                PlannedTrack = _stationA.Tracks[0],
                IsArrival = false,
                IsDeparture = true,
                SequenceNumber = 1
            },
            new(_train, _stationB, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(5),
                DepartureTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(5)
            })
            {
                Id = CallBId,
                PlannedTrack = _stationB.Tracks[0],
                IsArrival = true,
                IsDeparture = false,
                SequenceNumber = 2
            }
        };

        return Task.FromResult<IEnumerable<TrainStationCall>>(calls);
    }
}
