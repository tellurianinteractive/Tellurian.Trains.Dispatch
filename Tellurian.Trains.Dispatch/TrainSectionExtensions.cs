using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;


namespace Tellurian.Trains.Dispatch;

public static class TrainSectionExtensions
{
    extension(TrainSection trainSection)
    {
        public Station From => trainSection.StretchDirection.From;
        public Station To => trainSection.StretchDirection.To;
        public StretchDirection TrainDirection => trainSection.StretchDirection.Direction;
        internal Train Train => trainSection.Departure.Train;
        public bool IsFirst => trainSection.Departure.SequenceNumber == 1;
        public bool IsLast => trainSection.Arrival.IsDeparture == false;
        public bool IsUnmanned => trainSection.Train.State < TrainState.Manned;
        public bool IsVisibleForDispatcher => trainSection.State != DispatchState.Arrived;

    }
}
