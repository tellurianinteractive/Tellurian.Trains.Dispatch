using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Represents a specific direction of travel on a <see cref="DispatchStretch"/>.
/// Provides convenient access to origin/destination and direction-specific traversal.
/// </summary>
public class DispatchStretchDirection
{
    public DispatchStretch DispatchStretch { get; }
    public StretchDirection Direction { get; }

    /// <summary>
    /// Starting station for this direction.
    /// </summary>
    public Station From => Direction == StretchDirection.Forward
        ? DispatchStretch.From
        : DispatchStretch.To;

    /// <summary>
    /// Ending station for this direction.
    /// </summary>
    public Station To => Direction == StretchDirection.Forward
        ? DispatchStretch.To
        : DispatchStretch.From;

    /// <summary>
    /// Signal-controlled places along the route in this direction.
    /// </summary>
    public IEnumerable<SignalControlledPlace> IntermediateControlPoints =>
        Direction == StretchDirection.Forward
            ? DispatchStretch.IntermediateControlPoints
            : DispatchStretch.IntermediateControlPoints.Reverse();

    /// <summary>
    /// TrackStretches in order of traversal for this direction.
    /// </summary>
    public IEnumerable<TrackStretch> TrackStretchesInOrder =>
        Direction == StretchDirection.Forward
            ? DispatchStretch.TrackStretches
            : DispatchStretch.TrackStretches.Reverse();

    internal DispatchStretchDirection(DispatchStretch stretch, StretchDirection direction)
    {
        DispatchStretch = stretch;
        Direction = direction;
    }

    public override string ToString() => $"{From.Signature} → {To.Signature}";
    public override int GetHashCode() => HashCode.Combine(DispatchStretch.Id, Direction);
    public override bool Equals(object? obj) =>
        obj is DispatchStretchDirection other &&
        other.DispatchStretch.Id == DispatchStretch.Id &&
        other.Direction == Direction;
}

public static class DispatchStretchDirectionExtensions
{
    extension(DispatchStretchDirection stretch)
    {
        /// <summary>
        /// Checks if this stretch direction can have the given departure and arrival calls.
        /// </summary>
        internal bool CanHave(TrainStationCall departure, TrainStationCall arrival) =>
           departure.IsDeparture && arrival.IsArrival &&
           departure.Train.Id == arrival.Train.Id &&
           departure.At.Equals(stretch.From) && arrival.At.Equals(stretch.To);
    }
}
