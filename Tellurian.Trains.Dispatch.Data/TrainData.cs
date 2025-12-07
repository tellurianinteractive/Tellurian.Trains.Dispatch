using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data;

internal class TrainData
{
    public int Id { get; init; }
    public required Company Company { get; init; }
    public required Identity Identity { get; init; }
}

