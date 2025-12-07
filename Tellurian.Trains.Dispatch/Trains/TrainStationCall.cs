using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains;

public record TrainStationCall(Train Train, OperationPlace At, CallTime Scheduled)
{
    public int Id { get; set { field = value.OrNextId; } }
    public StationTrack Track => NewTrack is not null ? NewTrack : PlannedTrack;
    public StationTrack PlannedTrack { get; init; } = default!;
    public StationTrack? NewTrack { get; internal set; }
    public CallTime Observed { get; internal set; }
    public bool IsArrival { get; init; } = true;
    public bool IsDeparture { get; init; } = true;
    public int SequenceNumber { get; init; }
    public override string ToString() =>
        $"{At.Signature} track {PlannedTrack} arr {Scheduled.ArrivalTime:t} dep {Scheduled.DepartureTime:t}";
}
