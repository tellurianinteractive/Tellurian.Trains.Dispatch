using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;


namespace Tellurian.Trains.Dispatch;

public static class TrainSectionExtensions
{
    extension(TrainSection trainSection)
    {
        /// <summary>The place the train departs from in this section.</summary>
        public OperationPlace From => trainSection.Departure.At;
        /// <summary>The place the train arrives at, or leaves the dispatch stretch at, in this section.</summary>
        public OperationPlace To => trainSection.Arrival.At;
        public StretchDirection TrainDirection => trainSection.StretchDirection?.Direction ?? StretchDirection.Forward;
        internal Train Train => trainSection.Departure.Train;
        public bool IsFirst => trainSection.Previous is null;
        public bool IsLast => trainSection.Arrival.IsDeparture == false;
        public bool IsUnmanned => trainSection.Train.State < TrainState.Manned;
        public bool IsVisibleForDispatcher => trainSection.State != DispatchState.Arrived;

        /// <summary>
        /// True if Previous section has departed or arrived, allowing dispatch actions on this section.
        /// Always true for the first section (no previous).
        /// </summary>
        public bool IsPreviousDeparted =>
            trainSection.Previous is null ||
            trainSection.Previous.State >= DispatchState.Departed;

    }
}
