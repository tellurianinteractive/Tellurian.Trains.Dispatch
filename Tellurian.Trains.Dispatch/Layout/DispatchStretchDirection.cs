using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Layout;

public class DispatchStretchDirection
{
    public Station From { get; }
    public Station To { get; }
    public StretchDirection Direction { get; }

    public IList<BlockSignal> IntermediateBlockSignals;
    public DispatchStretch DispatchStretch { get; }

    internal DispatchStretchDirection(DispatchStretch stretch, Station from, Station to, StretchDirection direction, IList<BlockSignal>? intermediateBlockSignals = null)
    {
        DispatchStretch = stretch;
        From = from;
        To = to;
        Direction = direction;
        IntermediateBlockSignals = intermediateBlockSignals ?? [];
    }
    public override string ToString() => $"{From}-{To}, {BlocksOnSection}";
    public override int GetHashCode() => HashCode.Combine(From, To);
    public override bool Equals(object? obj) =>
        obj is DispatchStretchDirection stretch &&
        stretch.From.Equals(From) && stretch.To.Equals(To);

    private string BlocksOnSection => IntermediateBlockSignals.Count switch
    {
        0 => "No immediate block signals",
        _ => $"Block signals at {string.Join(", ", IntermediateBlockSignals.Select(ibs => ibs.Name))}",
    };
}

public static class DispatchStretchDirectionExtensions
{
    extension(DispatchStretchDirection stretch)
    {
        internal bool CanHave(TrainStationCall departure, TrainStationCall arrival) =>
           departure.IsDeparture && arrival.IsArrival &&
           departure.Train.Id == arrival.Train.Id &&
           departure.At == stretch.From && arrival.At == stretch.To;


    }


}
