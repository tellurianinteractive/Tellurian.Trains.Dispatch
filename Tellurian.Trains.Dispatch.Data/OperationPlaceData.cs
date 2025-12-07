using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data;

internal class OperationPlaceData
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Signature { get; init; }
    public bool IsManned { get; init; }
    public bool IsJunction { get; init; }
    public int? ControlledByDispatcherId { get; init; }
    public List<StationTrack> Tracks { get; init; } = [];
}

