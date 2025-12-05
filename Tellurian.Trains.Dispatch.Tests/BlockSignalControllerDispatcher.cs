namespace Tellurian.Trains.Dispatch.Tests;

/// <summary>
/// Reference to a dispatcher by name for BlockSignal.ControlledBy configuration.
/// Only the Name is used - matching is done by name against actual StationDispatcher instances.
/// </summary>
/// <remarks>
/// This is needed because BlockSignal is configured before the Broker creates StationDispatcher instances.
/// The actual dispatching uses StationDispatcher from Broker.GetDispatchers().
/// </remarks>
internal class BlockSignalControllerDispatcher(string name) : IDispatcher
{
    private static int _nextId = 1000;
    public int Id { get; } = Interlocked.Increment(ref _nextId);
    public string Name { get; } = name;
    public IEnumerable<TrainSection> Departures => throw new NotSupportedException("Use StationDispatcher from Broker");
    public IEnumerable<TrainSection> Arrivals => throw new NotSupportedException("Use StationDispatcher from Broker");
}
