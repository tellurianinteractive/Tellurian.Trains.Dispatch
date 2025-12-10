using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Test data provider for the Simple Test Case:
/// - Station A with tracks 1 and 2
/// - Station B with track 1
/// - Station C with tracks 10, 1 and 2
/// - TrackStretch A->B length 10m, single track
/// - TrackStretch B->C length 15m, single track
/// </summary>
/// <remarks>
/// Travel times calculated at 5m per minute:
/// - A to B: 10m = 2 minutes
/// - B to C: 15m = 3 minutes
/// </remarks>
internal sealed class SimpleTestDataProvider : IBrokerDataProvider
{
    // Stations - Id = 0 triggers auto-increment
    private readonly Station _stationA;
    private readonly Station _stationB;
    private readonly Station _stationC;

    // Track stretches - shared between GetTrackStretchesAsync and GetDispatchStretchesAsync
    private readonly TrackStretch _stretchAB;
    private readonly TrackStretch _stretchBC;

    // Company and trains
    private readonly Company _testCompany = new("Test Railway", "TR");

    private Train? _train1;

    public SimpleTestDataProvider()
    {
        // Create stations with auto-generated IDs
        _stationA = new("Station A", "A")
        {
            Id = 0, // Triggers auto-increment
            Tracks = [new StationTrack("1"), new StationTrack("2")]
        };

        _stationB = new("Station B", "B")
        {
            Id = 0,
            Tracks = [new StationTrack("1")]
        };

        _stationC = new("Station C", "C")
        {
            Id = 0,
            Tracks = [new StationTrack("10"), new StationTrack("1"), new StationTrack("2")]
        };

        // Create track stretches (single track - default is double-directed)
        _stretchAB = new TrackStretch(_stationA, _stationB) { Id = 0 };
        _stretchBC = new TrackStretch(_stationB, _stationC) { Id = 0 };
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<OperationPlace> places = [_stationA, _stationB, _stationC];
        return Task.FromResult(places);
    }

    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<TrackStretch> stretches = [_stretchAB, _stretchBC];
        return Task.FromResult(stretches);
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        var allTrackStretches = new List<TrackStretch> { _stretchAB, _stretchBC };

        var dispatchAB = new DispatchStretch(_stationA, _stationB, allTrackStretches) { Id = 0 };
        var dispatchBC = new DispatchStretch(_stationB, _stationC, allTrackStretches) { Id = 0 };

        IEnumerable<DispatchStretch> dispatches = [dispatchAB, dispatchBC];
        return Task.FromResult(dispatches);
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        _train1 = new Train(_testCompany, new Identity("P", 101)) { Id = 0 };

        IEnumerable<Train> trains = [_train1];
        return Task.FromResult(trains);
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        if (_train1 is null)
            throw new InvalidOperationException("GetTrainsAsync must be called before GetTrainStationCallsAsync");

        // Train 101: A -> B -> C
        // Departs A at 10:00, arrives B at 10:02, departs B at 10:03, arrives C at 10:06
        var calls = new List<TrainStationCall>
        {
            new(_train1, _stationA, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10),
                DepartureTime = TimeSpan.FromHours(10)
            })
            {
                Id = 0,
                PlannedTrack = _stationA.Tracks[0],
                IsArrival = false,
                IsDeparture = true,
                SequenceNumber = 1
            },
            new(_train1, _stationB, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(2),
                DepartureTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(3)
            })
            {
                Id = 0,
                PlannedTrack = _stationB.Tracks[0],
                IsArrival = true,
                IsDeparture = true,
                SequenceNumber = 2
            },
            new(_train1, _stationC, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(6),
                DepartureTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(6)
            })
            {
                Id = 0,
                PlannedTrack = _stationC.Tracks[1], // Track 1
                IsArrival = true,
                IsDeparture = false,
                SequenceNumber = 3
            }
        };

        return Task.FromResult<IEnumerable<TrainStationCall>>(calls);
    }
}
