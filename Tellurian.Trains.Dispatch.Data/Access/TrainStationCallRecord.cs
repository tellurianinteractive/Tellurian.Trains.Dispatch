using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;

internal class TrainStationCallRecord
{
    public int Id { get; init; }
    public int TrainId { get; init; }
    public int OperationPlaceId { get; init; }
    public int StationTrackId { get; init; }
    public CallTime Scheduled { get; init; }
    public bool IsArrival { get; set; } = true;
    public bool IsDeparture { get; set; } = true;
    internal int SequenceNumber { get; set; }
}

