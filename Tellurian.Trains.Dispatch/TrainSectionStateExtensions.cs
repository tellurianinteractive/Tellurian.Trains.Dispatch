using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

internal static class TrainSectionStateExtensions
{
    extension(TrainSection trainSection)
    {
        public bool IsVisibleForDispatcher => !trainSection.IsCompleted;
        public bool IsCompleted => trainSection.State == DispatchState.Arrived;
        public bool IsInProgress => trainSection.Train.IsNotCancelledOrAborted && trainSection.State != DispatchState.None;

        public DispatchState[] NextDispatchStates => trainSection.State switch
        {
            _ when trainSection.Train.IsCanceledOrAborted => [],
            DispatchState.None or
            DispatchState.Rejected or
            DispatchState.Revoked => [DispatchState.Requested],
            DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
            DispatchState.Accepted when trainSection.CannotProceed => [DispatchState.Revoked],
            DispatchState.Accepted => [DispatchState.Revoked, DispatchState.Departed],
            _ => []
        };

        /// <summary>
        /// States available for the arrival dispatcher.
        /// Arrived is only available after all block signals have been passed.
        /// </summary>
        public DispatchState[] ArrivalStates => trainSection.State switch
        {
            _ when trainSection.Train.IsCanceledOrAborted => [],
            DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
            DispatchState.Departed when trainSection.HasPassedAllBlockSignals => [DispatchState.Arrived],
            _ => []
        };

        /// <summary>
        /// States available for the departure dispatcher.
        /// </summary>
        public DispatchState[] DepartureStates => trainSection.State switch
        {
            _ when trainSection.Train.IsCanceledOrAborted => [],
            DispatchState.None or
            DispatchState.Rejected or
            DispatchState.Revoked => [DispatchState.Requested],
            DispatchState.Requested => [DispatchState.Revoked],
            DispatchState.Accepted => [DispatchState.Departed, DispatchState.Revoked],
            _ => []
        };

        public TrainState[] NextTrainStates => trainSection.Train.State switch
        {
            _ when trainSection.IsLast && trainSection.IsCompleted => [TrainState.Completed],
            TrainState.Planned => [TrainState.Manned, TrainState.Canceled],
            TrainState.Manned => [TrainState.Running, TrainState.Canceled],
            TrainState.Running => [TrainState.Aborted],
            _ => [],

        };

        public bool IsNext(DispatchState next) =>
            trainSection.NextDispatchStates.Contains(next);

        public bool IsNext(TrainState next) =>
            trainSection.NextTrainStates.Contains(next);

        public void SetDepartureTime() =>
            trainSection.Departure.SetDepartureTime(trainSection.TimeProvider.Time(trainSection.Departure.Scheduled.DepartureTime));
        public void SetArrivalTime() =>
            trainSection.Arrival.SetArrivalTime(trainSection.TimeProvider.Time(trainSection.Departure.Scheduled.ArrivalTime));
    }
}
