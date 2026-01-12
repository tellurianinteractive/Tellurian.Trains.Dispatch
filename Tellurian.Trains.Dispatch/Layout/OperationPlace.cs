using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;


namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Abstract base class for all locations where operational times can be recorded.
/// </summary>
/// <param name="Name">Full name of the place (e.g., "Stockholm Central").</param>
/// <param name="Signature">Short code/signature (e.g., "Cst").</param>
public abstract record OperationPlace(string Name, string Signature)
{
    /// <summary>
    /// Unique identifier, auto-generated if set to 0.
    /// </summary>
    public int Id { get; set { field = value.OrNextId; } }

    /// <summary>
    /// Tracks available at this place for trains to occupy.
    /// </summary>
    public IList<StationTrack> Tracks { get; init; } = [];

    /// <summary>
    /// True if this place is a controlled location (Station or SignalControlledPlace).
    /// </summary>
    public abstract bool IsControlled { get; }

    /// <summary>
    /// Two OperationalPlaces are equal if they have the same Id.
    /// This overrides record equality to avoid issues with list comparison.
    /// </summary>
    public virtual bool Equals(OperationPlace? other) =>
        other is not null && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => $"{Name} ({Signature})";
}
