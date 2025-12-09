using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;

internal class OperationPlaceRecord
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Signature { get; init; }
    public bool IsManned { get; init; }
    public bool IsJunction { get; init; }
    public int? ControlledByDispatcherId { get; init; }
    public List<StationTrack> Tracks { get; init; } = [];
}

internal class StationTrackRecord
{
    public int Id { get; init; }
    public int OperationPlaceId { get; init; }
    public required string TrackNumber { get; init; }
    public bool IsMainTrack { get; init; }
    public int DisplayOrder { get; init; }
    public int PlatformLength { get; init; }
}

