using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains;
/// <summary>
/// A <see cref="=DispatchStretch"/> defines the track properties between two adjacent <see cref="Station">stations</see> 
/// that have a <see cref="IDispatcher"/> role. 
/// There can be immediate 
/// </summary>
/// <param name="from">One end of the track stretch.</param>
/// <param name="to">The other end of the track stretch.</param>
/// <param name="numberOfTracks">The number of parallel tracks between the stations.</param>
/// <param name="intermediateBlockSignals">The immediate <see cref="BlockSignal">signals</see> on the <see cref="DispatchStretch"/>.</param>
/// <remarks>
/// The capacity of the track stretchs is defined of <see cref="numberOfTracks"/> and <see cref="numberOfBlocks"/>.
/// The <see cref="numberOfTracks"/>defines how many trains that concurrenlly can be sent in any direction.
/// The <see cref="intermediateBlockSignals"/>defines how many trains that concurrently can be sent on one track in the same direction.
/// Example: 
/// A track strectch has two <see cref="numberOfTracks"/> and two <see cref="BlockSignal"/>, i.e. one intermediate block signal.
/// Then on each track there can be two trains on its way in the same direction, totally four trains. 
/// On a double track this often means that there can be two trains on is way in each direction.
/// /// </remarks>
public class DispatchStretch(Station from, Station to, int numberOfTracks = 1, IList<BlockSignal>? intermediateBlockSignals = null)
{
    public Station From { get; } = from;
    public Station To { get; } = to;
    public int Direction { get; init; } = 1;
    public int NumberOfTracks { get; } = numberOfTracks;
    public DispatchStretch Reverse { get; } = new DispatchStretch(to, from, numberOfTracks, intermediateBlockSignals?.Reversed) { Direction = 2 };

    public IList<BlockSignal> IntermediateBlockSignals = intermediateBlockSignals ?? [];
    public IList<TrainStretch> ActiveTrains { get; } = [];
    public override string ToString() => $"{From}-{To} {Tracks}, {BlocksOnSection}";
    public override int GetHashCode() => HashCode.Combine(From, To);
    public override bool Equals(object? obj) => obj is DispatchStretch stretch && stretch.From.Equals(From) && stretch.To.Equals(To);

    public bool TryAddActiveDispatchTrain(TrainStretch trainStretch)
    {
        if (this.HasFreeTrackFor(trainStretch))
        {
            ActiveTrains.Add(trainStretch);
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

    private string BlocksOnSection => IntermediateBlockSignals.Count switch
    {
        0 => "No immediate blocks",
        1 => "One immediate block",
        _ => $"{IntermediateBlockSignals.Count} intermediate blocks"

    };
}
