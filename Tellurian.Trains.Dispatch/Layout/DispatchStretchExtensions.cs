using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Layout;

public static class DispatchStretchExtensions
{
    extension(DispatchStretch stretch)
    {
        internal bool CanHave(TrainStationCall departure, TrainStationCall arrival) =>
            stretch.Forward.CanHave(departure, arrival) ||
            stretch.Reverse.CanHave(departure, arrival);

        internal bool IsForward(DispatchStretchDirection direction) => direction.Equals(stretch.Forward); 
        internal bool IsReverse(DispatchStretchDirection direction) => direction.Equals(stretch.Reverse);

        /// <summary>
        /// Checks if a train can enter the stretch (depart into Block 0).
        /// For single track: no opposite-direction trains allowed.
        /// For all tracks: Block 0 must be free in the train's direction.
        /// </summary>
        internal bool HasFreeTrackFor(TrainSection trainSection)
        {
            var direction = trainSection.TrainDirection;
            var trainsInSameDirection = direction.IsForward ? stretch.TrainsInForwardDirection : stretch.TrainsInReverseDirection;
            var trainsInOppositeDirection = direction.IsReverse ? stretch.TrainsInForwardDirection : stretch.TrainsInReverseDirection;

            // Single track: no opposite-direction trains allowed (collision risk)
            if (stretch.NumberOfTracks == 1 && trainsInOppositeDirection.Length > 0)
                return false;

            // Block 0 must be free (no train currently in first segment)
            return !trainsInSameDirection.Any(t => t.CurrentBlockIndex == 0);
        }

        /// <summary>
        /// Checks if a specific block is free for trains in a given direction.
        /// </summary>
        internal bool IsBlockFree(int blockIndex, int direction)
        {
            var trainsInDirection = direction == 1 ? stretch.TrainsInForwardDirection : stretch.TrainsInReverseDirection;
            return !trainsInDirection.Any(t => t.CurrentBlockIndex == blockIndex);
        }

        internal TrainSection[] TrainsInForwardDirection =>
            [.. stretch.ActiveTrains.Where(t => t.IsRunningForwardOn(stretch))];
        internal TrainSection[] TrainsInReverseDirection =>
            [.. stretch.ActiveTrains.Where(t => t.IsRunningReverseOn(stretch))];
    }

    extension(IEnumerable<DispatchStretch> stretches)
    {
        internal DispatchStretch FindFor(TrainStationCall departure, TrainStationCall arrival) =>
            stretches.Single(s => s.Forward.CanHave(departure, arrival) || s.Reverse.CanHave(departure, arrival));
    }

    extension(TrainSection trainSection)
    {
        internal bool IsOn(DispatchStretch dispatchStretch) =>
            trainSection.Departure.At.Equals(dispatchStretch.Forward.From) && trainSection.Arrival.At.Equals(dispatchStretch.Forward.To) ||
            trainSection.Departure.At.Equals(dispatchStretch.Reverse.From) && trainSection.Arrival.At.Equals(dispatchStretch.Reverse.To);
    }
}