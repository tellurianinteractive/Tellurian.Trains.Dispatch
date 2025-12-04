namespace Tellurian.Trains.Dispatch.Trains;

public static class DispatchStretchExtensions
{
    extension(DispatchStretch stretch)
    {
        internal bool CanHave(TrainStationCall departure, TrainStationCall arrival) =>
            departure.IsDeparture && arrival.IsArrival &&
            departure.Train.Id == arrival.Train.Id &&
            departure.At == stretch.From && arrival.At == stretch.To;

        internal bool IsReverse => stretch.Equals(stretch.Reverse);

        /// <summary>
        /// Checks if a train can enter the stretch (depart into Block 0).
        /// For single track: no opposite-direction trains allowed.
        /// For all tracks: Block 0 must be free in the train's direction.
        /// </summary>
        internal bool HasFreeTrackFor(TrainStretch trainStretch)
        {
            var direction = trainStretch.TrainDirection;
            var trainsInSameDirection = direction == 1 ? stretch.TrainsInForwardDirection : stretch.TrainsInReverseDirection;
            var trainsInOppositeDirection = direction == 1 ? stretch.TrainsInReverseDirection : stretch.TrainsInForwardDirection;

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

        internal TrainStretch[] TrainsInForwardDirection =>
            [.. stretch.ActiveTrains.Where(t => t.IsRunningForwardOn(stretch))];
        internal TrainStretch[] TrainsInReverseDirection =>
            [.. stretch.ActiveTrains.Where(t => t.IsRunningReverseOn(stretch))];
    }

    extension(IEnumerable<DispatchStretch> stretches)
    {
        internal DispatchStretch FindFor(TrainStationCall departure, TrainStationCall arrival) =>
            stretches.Single(s => s.CanHave(departure, arrival) || s.Reverse.CanHave(departure, arrival));
    }

    extension(TrainStretch trainStretch)
    {
        internal bool IsOn(DispatchStretch dispatchStretch) =>
            trainStretch.Departure.At.Equals(dispatchStretch.From) && trainStretch.Arrival.At.Equals(dispatchStretch.To) ||
            trainStretch.Departure.At.Equals(dispatchStretch.Reverse.From) && trainStretch.Arrival.At.Equals(dispatchStretch.Reverse.To);
    }
}