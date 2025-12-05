using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains;

public record Station(string Name,string Signature,  bool IsManned = true)
{
    public int Id { get; set { field = value.OrNextId; } }
    public override string ToString() => $"{Name} ({Signature})";

    public IDispatcher Dispatcher { get; internal set; } = default!;
}

public static class StationExtensions
{

}
