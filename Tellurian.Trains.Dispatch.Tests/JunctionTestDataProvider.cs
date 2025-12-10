using Tellurian.Trains.Dispatch.Brokers;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Test data provider for the Junction Test Case:
/// - Station A with tracks 1 and 2
/// - SignalControlledPlace B (junction, controlled by Station A)
/// - Station C with tracks 10, 1 and 2
/// - Station D with tracks 1-5
/// - TrackStretch A->B length 10m, single track
/// - TrackStretch B->C length 15m, single track
/// - TrackStretch B->D length 8m, single track
/// </summary>
/// <remarks>
/// Layout:
///     A ----\
///            B ---- C
///     D ----/
///
/// Train routes:
/// - Train 201 (A-B-C): Departs A at 10:00, passes B, arrives C at 10:05
/// - Train 202 (D-B-A): Departs D at 10:00, passes B, arrives A at 10:04
///
/// Test focus: Train D-B-A can depart from D but cannot pass B until
/// train A-B-C has passed B. Both trains have the same scheduled time at B.
/// </remarks>
internal sealed class JunctionTestDataProvider : IBrokerDataProvider
{
    // Stations
    private readonly Station _stationA;
    private readonly Station _stationC;
    private readonly Station _stationD;

    // Signal controlled place - junction controlled by Station A
    private readonly SignalControlledPlace _signalB;

    // Track stretches
    private readonly TrackStretch _stretchAB;
    private readonly TrackStretch _stretchBC;
    private readonly TrackStretch _stretchBD;

    // Company and trains
    private readonly Company _testCompany = new("Test Railway", "TR");
    private Train? _train1; // A-B-C
    private Train? _train2; // D-B-A

    public JunctionTestDataProvider()
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

        _stationD = new("Station D", "D")
        {
            Id = 0,
            Tracks = [new StationTrack("1"), new StationTrack("2"), new StationTrack("3"), new StationTrack("4"), new StationTrack("5")]
        };

        // SignalControlledPlace B is controlled by Station A
        _signalB = new("Signal B", "B", _stationA.Id)
        {
            Id = 0
        };

        // Create track stretches (single track)
        _stretchAB = new TrackStretch(_stationA, _signalB) { Id = 0 };
        _stretchBC = new TrackStretch(_signalB, _stationC) { Id = 0 };
        _stretchBD = new TrackStretch(_signalB, _stationD) { Id = 0 };
    }

    public Task<IEnumerable<OperationPlace>> GetOperationPlacesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<OperationPlace> places = [_stationA, _signalB, _stationC, _stationD];
        return Task.FromResult(places);
    }

    public Task<IEnumerable<TrackStretch>> GetTrackStretchesAsync(CancellationToken cancellationToken = default)
    {
        IEnumerable<TrackStretch> stretches = [_stretchAB, _stretchBC, _stretchBD];
        return Task.FromResult(stretches);
    }

    public Task<IEnumerable<DispatchStretch>> GetDispatchStretchesAsync(CancellationToken cancellationToken = default)
    {
        // Dispatch stretch from A to C (passing through B)
        var dispatchAC = new DispatchStretch(_stationA, _stationC, [_stretchAB, _stretchBC]) { Id = 0 };

        // Dispatch stretch from D to A (passing through B)
        // Note: Train travels D -> B -> A, using stretches BD and AB in reverse direction
        var dispatchDA = new DispatchStretch(_stationD, _stationA, [_stretchBD, _stretchAB]) { Id = 0 };

        IEnumerable<DispatchStretch> dispatches = [dispatchAC, dispatchDA];
        return Task.FromResult(dispatches);
    }

    public Task<IEnumerable<Train>> GetTrainsAsync(CancellationToken cancellationToken = default)
    {
        _train1 = new Train(_testCompany, new Identity("P", 201)) { Id = 0 }; // A-B-C
        _train2 = new Train(_testCompany, new Identity("P", 202)) { Id = 0 }; // D-B-A

        IEnumerable<Train> trains = [_train1, _train2];
        return Task.FromResult(trains);
    }

    public Task<IEnumerable<TrainStationCall>> GetTrainStationCallsAsync(CancellationToken cancellationToken = default)
    {
        if (_train1 is null || _train2 is null)
            throw new InvalidOperationException("GetTrainsAsync must be called before GetTrainStationCallsAsync");

        var calls = new List<TrainStationCall>
        {
            // Train 201: A -> C (passing through signal B)
            // Departs A at 10:00, arrives C at 10:05
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
            },

            // Train 202: D -> A (passing through signal B)
            // Departs D at 10:00, arrives A at 10:04
            // Both trains are scheduled to pass B at approximately the same time
            new(_train2, _stationD, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10),
                DepartureTime = TimeSpan.FromHours(10)
            })
            {
                Id = 0,
                PlannedTrack = _stationD.Tracks[0],
                IsArrival = false,
                IsDeparture = true,
                SequenceNumber = 1
            },
            new(_train2, _stationA, new CallTime
            {
                ArrivalTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(4),
                DepartureTime = TimeSpan.FromHours(10) + TimeSpan.FromMinutes(4)
            })
            {
                Id = 0,
                PlannedTrack = _stationA.Tracks[1], // Track 2
                IsArrival = true,
                IsDeparture = false,
                SequenceNumber = 2
            }
        };

        return Task.FromResult<IEnumerable<TrainStationCall>>(calls);
    }
}
