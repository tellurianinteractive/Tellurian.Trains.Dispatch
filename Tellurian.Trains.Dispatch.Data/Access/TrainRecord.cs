using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;

internal class TrainRecord
{
    public int Id { get; init; }
    public required Company Company { get; init; }
    public required Identity Identity { get; init; }
}

