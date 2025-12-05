using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Layout;
/// <summary>
/// A <see cref="=DispatchStretch"/> defines the track properties between two adjacent <see cref="Station">stations</see> 
/// that have a <see cref="IDispatcher"/> role. 
/// There can be immediate 
/// </summary>
/// <remarks>
/// The capacity of the track stretchs is defined of <see cref="numberOfTracks"/> and <see cref="numberOfBlocks"/>.
/// The <see cref="numberOfTracks"/>defines how many trains that concurrenlly can be sent in any direction.
/// The <see cref="intermediateBlockSignals"/>defines how many trains that concurrently can be sent on one track in the same direction.
/// Example: 
/// A track strectch has two <see cref="numberOfTracks"/> and two <see cref="BlockSignal"/>, i.e. one intermediate block signal.
/// Then on each track there can be two trains on its way in the same direction, totally four trains. 
/// On a double track this often means that there can be two trains on is way in each direction.
/// /// </remarks>
public class DispatchStretch
{
    public int NumberOfTracks { get; }
    public DispatchStretchDirection Forward { get; }
    public DispatchStretchDirection Reverse { get; }

    /// <param name="from">One end of the track stretch.</param>
    /// <param name="to">The other end of the track stretch.</param>
    /// <param name="numberOfTracks">The number of parallel tracks between the stations.</param>
    /// <param name="intermediateBlockSignals">The immediate <see cref="BlockSignal">signals</see> on the <see cref="DispatchStretch"/>.</param>
      public DispatchStretch(Station from, Station to, int numberOfTracks = 1, IList<BlockSignal>? intermediateBlockSignals = null)
    {
        NumberOfTracks = numberOfTracks;
        Forward = new(this, from, to, StretchDirection.Forward, intermediateBlockSignals);
        Reverse = new(this, to, from, StretchDirection.Reverse, intermediateBlockSignals?.Reversed);
    }

    public IList<TrainSection> ActiveTrains { get; } = [];
    public override string ToString() => $"{Forward}, {Tracks}";
    public override int GetHashCode() => HashCode.Combine(Forward, Reverse);
    public override bool Equals(object? obj) => obj is DispatchStretch stretch && stretch.Forward.Equals(Forward) && stretch.Reverse.Equals(Reverse);

    public bool TryAddActiveDispatchTrain(TrainSection trainSection)
    {
        if (this.HasFreeTrackFor(trainSection))
        {
            ActiveTrains.Add(trainSection);
            return true;
        }
        return false;
    }

    private string Tracks => NumberOfTracks switch
    {
        1 => "Single track",
        2 => "Double track",
        _ => $"{NumberOfTracks} parallel tracks"
    };
}
