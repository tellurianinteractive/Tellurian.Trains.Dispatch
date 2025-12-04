using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains;

public record TrainStationCall(Train Train, Station At, CallTime Scheduled)
{
    public int Id { get; set { field = value.OrNextId; } }
    public StationTrack? Track { get; set; }
    public CallTime Observed { get; internal set; }
    public bool IsArrival { get; set; } = true;
    public bool IsDeparture { get; set; } = true;
    internal int SequenceNumner { get; set; }
    public override string ToString() => $"{At.Signature} track {Track} arr {Scheduled.ArrivalTime:t} dep {Scheduled.DepartureTime:t}";
}
