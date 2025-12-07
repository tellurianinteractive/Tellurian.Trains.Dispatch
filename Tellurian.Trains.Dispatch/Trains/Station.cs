using Tellurian.Trains.Dispatch.Layout;

namespace Tellurian.Trains.Dispatch.Trains;

/// <summary>
/// A station is a manned or unmanned place where trains can stop,
/// passengers can board/alight, and dispatching operations are performed.
/// </summary>
/// <param name="Name">Full name of the station (e.g., "Stockholm Central").</param>
/// <param name="Signature">Short code (e.g., "Cst").</param>
/// <param name="IsManned">True if the station has a dispatcher on duty.</param>
public record Station(string Name, string Signature)
    : OperationPlace(Name, Signature)
{
    /// <summary>
    /// The dispatcher responsible for this station.
    /// Set during Broker initialization.
    /// </summary>
    public IDispatcher Dispatcher { get; internal set; } = default!;
    public bool IsManned { get; init; } = true;
    public override bool IsControlled => true;
}

