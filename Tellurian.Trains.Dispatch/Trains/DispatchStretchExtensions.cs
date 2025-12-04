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

        internal bool HasFreeTrackFor(TrainStretch trainStretch)
        {
            var direction = trainStretch.TrainDirection;
            var currentForwardTrains = stretch.TrainsInForwardDirection;
            var currentReverseTrains = stretch.TrainsInReverseDirection;

            if (stretch.NumberOfTracks == 1)
            {
                if (currentForwardTrains.Length > 0 || currentReverseTrains.Length > 0) return false;// Trains will collide!!
                if (direction == 1 && currentForwardTrains.Length < stretch.IntermediateBlockSignals.Count + 1) return true;
                if (direction == 2 && currentReverseTrains.Length < stretch.IntermediateBlockSignals.Count + 1) return true;
            }
            else if (stretch.NumberOfTracks == 2) 
            {
                if (direction == 1 && currentForwardTrains.Length < stretch.IntermediateBlockSignals.Count + 1) return true;
                if (direction == 2 && currentReverseTrains.Length < stretch.IntermediateBlockSignals.Count + 1) return true;
            }
            return false;
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
        public bool IsOn(DispatchStretch dispatchStretch) =>
            trainStretch.Departure.At.Equals(dispatchStretch.From) && trainStretch.Arrival.At.Equals(dispatchStretch.To) ||
            trainStretch.Departure.At.Equals(dispatchStretch.Reverse.From) && trainStretch.Arrival.At.Equals(dispatchStretch.Reverse.To);
    }
}