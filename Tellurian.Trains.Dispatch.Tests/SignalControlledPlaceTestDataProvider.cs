using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Test data provider for the Signal Controlled Places Test Case:
/// - Station A with tracks 1 and 2
/// - SignalControlledPlace B (controlled by Station A)
/// - Station C with tracks 10, 1 and 2
/// - TrackStretch A->B length 10m, single track
/// - TrackStretch B->C length 15m, single track
/// </summary>
/// <remarks>
/// This creates a single DispatchStretch from A to C that passes through
/// SignalControlledPlace B. The Pass action tests the intermediate signal passage.
/// Travel times: A to B: 2 min, B to C: 3 min
/// </remarks>
internal class SignalControlledPlaceTestDataProvider : IBrokerDataProvider
{
    // Stations
    private readonly Station _stationA;
    private readonly Station _stationC;

    // Signal controlled place - controlled by Station A
    private readonly SignalControlledPlace _signalB;

    // Track stretches
    private readonly TrackStretch _stretchAB;
    private readonly TrackStretch _stretchBC;

    // Company and trains
    private readonly Company _testCompany = new("Test Railway", "TR");
    private Train? _train1;

    public SignalControlledPlaceTestDataProvider()
    {
        // Create stations with auto-generated IDs
        _stationA = new("Station A", "A")
        {
            Id = 0,
            Tracks = [new StationTrack("1"), new StationTrack("2")]
        };

        _stationC = new("Station C", "C")
        {
            Id = 0,
            Tracks = [new StationTrack("10"), new StationTrack("1"), new StationTrack("2")]
        };

        // SignalControlledPlace B is controlled by Station A
        // Note: ControlledByDispatcherId uses Station A's Id (which will be set after auto-increment)
        _signalB = new("Signal B", "B", _stationA.Id)
        {
            Id = 0
        };

        // Create track stretches (single track)
        _stretchAB = new TrackStretch(_stationA, _signalB) { Id = 0 };
        _stretchBC = new TrackStretch(_signalB, _stationC) { Id = 0 };
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<OperationPlace> places = [_stationA, _signalB, _stationC];
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

        // Single dispatch stretch from A to C (passing through B)
        var dispatchAC = new DispatchStretch(_stationA, _stationC, allTrackStretches) { Id = 0 };

        IEnumerable<DispatchStretch> dispatches = [dispatchAC];
        return Task.FromResult(dispatches);
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        _train1 = new Train(_testCompany, new Identity("P", 201)) { Id = 0 };

        IEnumerable<Train> trains = [_train1];
        return Task.FromResult(trains);
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        if (_train1 is null)
            throw new InvalidOperationException("GetTrainsAsync must be called before GetTrainStationCallsAsync");

        // Train 201: A -> C (passing through signal B)
        // Departs A at 10:00, arrives C at 10:05
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
            new(_train1, _stationC, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(5),
                DepartureTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(5)
            })
            {
                Id = 0,
                PlannedTrack = _stationC.Tracks[1], // Track 1
                IsArrival = true,
                IsDeparture = false,
                SequenceNumber = 2
            }
        };

        return Task.FromResult<IEnumerable<TrainStationCall>>(calls);
    }
}
