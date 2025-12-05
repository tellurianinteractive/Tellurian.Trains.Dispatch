using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

public static class TrainStretchExtensions
{
    extension(TrainStretch trainStretch)
    {
        public Station From => trainStretch.From;
        public Station To => trainStretch.To;
        public TimeSpan DepartureTime => trainStretch.Departure.Scheduled.DepartureTime;
        public TimeSpan ArrivalTime => trainStretch.Arrival.Scheduled.ArrivalTime;
        public int TrainDirection => trainStretch.DispatchStretch.IsReverse ? 2 : 1;
        internal Train Train => trainStretch.Departure.Train;
        public bool IsFirst => trainStretch.Departure.SequenceNumner == 1;
        public bool IsLast => trainStretch.Arrival.IsDeparture == false;

        internal bool IsRunningForwardOn(DispatchStretch dispatchStretch) =>
             trainStretch.Departure.At.Equals(dispatchStretch.From) && trainStretch.Arrival.At.Equals(dispatchStretch.To);

        internal bool IsRunningReverseOn(DispatchStretch dispatchStretch) =>
            trainStretch.Departure.At.Equals(dispatchStretch.Reverse.From) && trainStretch.Arrival.At.Equals(dispatchStretch.Reverse.To);

        internal bool CanProceed =>
            trainStretch.DispatchStretch.HasFreeTrackFor(trainStretch);

        internal bool CannotProceed =>
            !trainStretch.CanProceed;
    }
}
