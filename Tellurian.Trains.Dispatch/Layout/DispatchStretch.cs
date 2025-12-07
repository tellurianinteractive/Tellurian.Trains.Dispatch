using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// A <see cref="DispatchStretch"/> defines a logical route between two <see cref="Station">stations</see>,
/// composed of one or more <see cref="TrackStretch">track stretches</see>.
/// Capacity is managed at the TrackStretch level, allowing shared infrastructure
/// between multiple DispatchStretches.
/// </summary>
/// <param name="from">The <see cref="Station"/> that is one end</param>
/// <param name="to">The <see cref="Station"/> that is the other end.</param>
/// <param name="allTrackStretches">
/// contains all track stretches in the layout, and they should be defines in
/// one primary direction so that it is possible to find the shortest path between stations.
/// </param>
/// <remarks>
/// The <see cref="TrackStretches"/> are created using a shortest path algorithm.
/// that finds the sequence of track stretches between from and to stations.
/// </remarks>
public class DispatchStretch
{
    public DispatchStretch(Station from, Station to, IEnumerable<TrackStretch> allTrackStretces)
    {
        From = from;
        To = to;
        TrackStretches = allTrackStretces.FindPathBetween(from, to);
        Forward = new(this, StretchDirection.Forward);
        Reverse = new(this, StretchDirection.Reverse);
    }
    public int Id { get; set { field = value.OrNextId; } }

    /// <summary>
    /// Ordered sequence of TrackStretches that make up this DispatchStretch.
    /// Found using shortest path algorithm between origin and destination.
    /// </summary>
    public IList<TrackStretch> TrackStretches { get; }

    /// <summary>
    /// Forward direction descriptor (Origin → Destination).
    /// </summary>
    public DispatchStretchDirection Forward { get; }

    /// <summary>
    /// Reverse direction descriptor (Destination → Origin).
    /// </summary>
    public DispatchStretchDirection Reverse { get; }


    /// <summary>
    /// Creates a DispatchStretch from a sequence of TrackStretches.
    /// </summary>
    /// <param name="trackStretches">Ordered sequence of TrackStretches from origin to destination.</param>

    /// <summary>
    /// Origin station of this dispatch stretch.
    /// </summary>
    public Station From { get; }

    /// <summary>
    /// Destination station of this dispatch stretch.
    /// </summary>
    public Station To { get; }

    /// <summary>
    /// Signal-controlled places along the route (intermediate points between TrackStretches).
    /// </summary>
    public IEnumerable<SignalControlledPlace> IntermediateControlPoints =>
        TrackStretches.Skip(1).Select(s => s.Start).OfType<SignalControlledPlace>();

    /// <summary>
    /// Active train sections currently using this dispatch stretch.
    /// </summary>
    public IList<TrainSection> ActiveTrains { get; } = [];

    public override string ToString() => $"{From.Signature} → {To.Signature}";
    public override int GetHashCode() => Id.GetHashCode();
    public override bool Equals(object? obj) => obj is DispatchStretch stretch && stretch.Id == Id;
}
