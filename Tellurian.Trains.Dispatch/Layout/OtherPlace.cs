namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// A place without signal control. Used for:
/// <list type="bullet">
/// <item>
/// <term>Simple halt</term>
/// <description>
/// A passenger stop without signalling (<see cref="IsJunction"/> = false).
/// Trains CAN stop here for scheduled passenger service.
/// </description>
/// </item>
/// <item>
/// <term>Unsignalled junction</term>
/// <description>
/// A junction point without signals (<see cref="IsJunction"/> = true).
/// Has diverging routes but no signal protection.
/// </description>
/// </item>
/// </list>
/// <para>
/// <strong>Cascading occupancy</strong> applies to ALL OtherPlaces since there are no signals.
/// When a train enters a TrackStretch ending at an OtherPlace, ALL connected
/// TrackStretches from that place are also occupied until the train
/// reaches a Station or SignalControlledPlace.
/// </para>
/// </summary>
/// <param name="Name">Name of the place (e.g., "Halt Mora", "Junction J1").</param>
/// <param name="Signature">Short code (e.g., "HM", "J1").</param>
public record OtherPlace(string Name, string Signature) : OperationPlace(Name, Signature)
{
    /// <summary>
    /// True if this is an unsignalled junction with diverging routes.
    /// False for simple halts on a single line.
    /// </summary>
    public bool IsJunction { get; init; }

    /// <summary>
    /// Always true - OtherPlaces have no signals, so cascading occupancy rules apply.
    /// </summary>
    public bool RequiresCascadingOccupancy => true;

    public override bool IsControlled => false;

    public override string ToString() =>
        IsJunction
            ? $"{Name} ({Signature}), unsignalled junction"
            : $"{Name} ({Signature}), halt";
}
