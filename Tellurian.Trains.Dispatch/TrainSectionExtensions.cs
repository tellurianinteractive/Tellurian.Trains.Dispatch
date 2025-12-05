using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

public static class TrainSectionExtensions
{
    extension(TrainSection trainSection)
    {
        public Station From => trainSection.StretchDirection.From;
        public Station To => trainSection.StretchDirection.To;
        public TimeSpan DepartureTime => trainSection.Departure.Scheduled.DepartureTime;
        public TimeSpan ArrivalTime => trainSection.Arrival.Scheduled.ArrivalTime;
        public StretchDirection TrainDirection => trainSection.StretchDirection.Direction;
        internal Train Train => trainSection.Departure.Train;
        public bool IsFirst => trainSection.Departure.SequenceNumner == 1;
        public bool IsLast => trainSection.Arrival.IsDeparture == false;

        internal bool IsRunningForwardOn(DispatchStretch dispatchStretch) =>
             trainSection.Departure.At.Equals(dispatchStretch.Forward.From) && trainSection.Arrival.At.Equals(dispatchStretch.Forward.To);

        internal bool IsRunningReverseOn(DispatchStretch dispatchStretch) =>
            trainSection.Departure.At.Equals(dispatchStretch.Reverse.From) && trainSection.Arrival.At.Equals(dispatchStretch.Reverse.To);

        internal bool CanProceed =>
            trainSection.DispatchStretch.HasFreeTrackFor(trainSection);

        internal bool CannotProceed =>
            !trainSection.CanProceed;
    }
}
