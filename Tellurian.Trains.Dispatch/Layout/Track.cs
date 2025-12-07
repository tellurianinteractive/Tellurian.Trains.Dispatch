namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Represents a physical track on a <see cref="TrackStretch"/>.
/// A TrackStretch can have multiple tracks (e.g., double-track line).
/// </summary>
/// <param name="Number">Optional track number/designation (e.g., "1", "2", "U", "N").</param>
/// <param name="Direction">The operational direction(s) this track can be used for.</param>
public record Track(string? Number, TrackOperationDirection Direction)
{
    /// <summary>
    /// True if this is the "Up" track, preferred for trains going in the From→To direction.
    /// On a double-track line, typically one track is Up and one is Down.
    /// </summary>
    public bool IsUpTrack { get; init; }
    public bool IsDownTrack => !IsUpTrack;

    /// <summary>
    /// Returns true if the track can be used in the forward direction (From→To).
    /// </summary>
    public bool AllowsForward => Direction.AllowsForward;

    /// <summary>
    /// Returns true if the track can be used in the backward direction (To→From).
    /// </summary>
    public bool AllowsBackward => Direction.AllowsBackward;

    public override string ToString() =>
        Number is null
            ? $"{Direction}"
            : $"Track {Number} ({Direction})";
}
