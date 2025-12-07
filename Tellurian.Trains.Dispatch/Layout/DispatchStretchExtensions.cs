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

        public List<TrackStretch> FindPathBetween(Station from, Station to)
        {
            if (from.Equals(to)) return [];

            // Build adjacency: only follow TrackStretch in its defined direction (Start → End)
            // This ensures trains move consistently in same dirction over track streches and validates import data correctness
            var adjacency = new Dictionary<OperationPlace, List<(TrackStretch Stretch, OperationPlace Next)>>();
            foreach (var ts in trackStretches)
            {
                if (!adjacency.ContainsKey(ts.Start)) adjacency[ts.Start] = [];
                adjacency[ts.Start].Add((ts, ts.End));
            }

            if (!adjacency.ContainsKey(from)) return [];

            // BFS: track how we reached each place
            var visited = new HashSet<OperationPlace> { from };
            var queue = new Queue<OperationPlace>();
            queue.Enqueue(from);
            var cameFrom = new Dictionary<OperationPlace, (OperationPlace Previous, TrackStretch Via)>();

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.Equals(to))
                {
                    // Reconstruct path
                    var path = new List<TrackStretch>();
                    OperationPlace node = to;
                    while (cameFrom.ContainsKey(node))
                    {
                        var (previous, via) = cameFrom[node];
                        path.Add(via);
                        node = previous;
                    }
                    path.Reverse();
                    return path;
                }

                if (!adjacency.TryGetValue(current, out var neighbors)) continue;
                foreach (var (stretch, next) in neighbors)
                {
                    if (visited.Add(next))
                    {
                        cameFrom[next] = (current, stretch);
                        queue.Enqueue(next);
                    }
                }
            }

            return []; // No path found
        }
    }

}