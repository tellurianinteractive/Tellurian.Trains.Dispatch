namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// A place with signal control, typically a block signal or passing loop.
/// Replaces the former <c>BlockSignal</c> concept with richer semantics.
/// </summary>
/// <remarks>
/// <para>
/// A SignalControlledPlace operates in two modes:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>Pass-through mode</term>
/// <description>
/// When no train meet is scheduled, trains simply pass through.
/// The dispatcher confirms passage with a "Pass" action.
/// </description>
/// </item>
/// <item>
/// <term>Meeting mode</term>
/// <description>
/// When trains meet here, the place functions like a mini-station.
/// Both trains must "Arrive" before either can "Depart".
/// Requires multiple tracks (<see cref="OperationPlace.Tracks"/>).
/// </description>
/// </item>
/// </list>
/// </remarks>
/// <param name="Name">Name of the signal/place (e.g., "Signal 1", "Passing Loop Mora").</param>
/// <param name="Signature">Short code (e.g., "S1", "PLM").</param>
/// <param name="ControlledByDispatcherId">Id of <see cref="IDispatcher"/> and should be resoved during initialisation.</param>
public record SignalControlledPlace(string Name, string Signature, int ControlledByDispatcherId)
    : OperationPlace(Name, Signature)
{
    public IDispatcher ControlledBy { get; internal set; } = default!;
    /// <summary>
    /// True if this is a junction point with diverging routes.
    /// </summary>
    public bool IsJunction { get; init; }

    /// <summary>
    /// True if train meets can occur here (has multiple tracks).
    /// </summary>
    public bool CanHostMeeting => Tracks.Count > 1;

    public override bool IsControlled => true;

    public override string ToString() =>
        IsJunction
            ? $"{Name} ({Signature}), junction controlled by {ControlledBy.Name}"
            : $"{Name} ({Signature}) controlled by {ControlledBy.Name}";
}
