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
        /// Total number of TrackStretches in this dispatch stretch.
        /// </summary>
        internal int NumberOfTrackStretches => stretch.TrackStretches.Count;

        /// <summary>
        /// Checks if a train can enter the stretch (depart into first TrackStretch).
        /// Capacity checking is done per TrackStretch in the new model.
        /// </summary>
        internal bool HasFreeTrackFor(TrainSection trainSection)
        {
            var direction = trainSection.TrainDirection;
            var firstTrackStretch = direction.IsForward
                ? stretch.TrackStretches.First()
                : stretch.TrackStretches.Last();

            // Check if the first TrackStretch has available capacity
            return firstTrackStretch.HasAvailableTrack(direction);
        }
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

    extension(IEnumerable<TrackStretch> trackStretches)
    {

        public List<TrackStretch> FindPathBetween(Station From, Station To)
        {
            // Simple case
            var result = (trackStretches.SingleOrDefault(ts => ts.Start.Equals(From) && ts.End.Equals(To)));
            if (result is not null) return [result];
            // TODO: Otherwise implement shortest path algoritm. The trackStretches dictionary has Start station Id as key.
            return [.. trackStretches]; //TODO: Temporary return to avoid compilation error, replace with actual result.
        }
    }

}