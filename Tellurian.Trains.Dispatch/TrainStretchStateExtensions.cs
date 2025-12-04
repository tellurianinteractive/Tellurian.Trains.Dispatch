using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

internal static class TrainStretchStateExtensions
{
    extension(TrainStretch trainStretch)
    {
        public bool IsVisibleForDispatcher => !trainStretch.IsCompleted;
        public bool IsCompleted => trainStretch.State == DispatchState.Arrived;
        public bool IsInProgress => trainStretch.Train.IsNotCancelledOrAborted && trainStretch.State != DispatchState.None;

        public DispatchState[] NextDispatchStates => trainStretch.State switch
        {
            _ when trainStretch.Train.IsCanceledOrAborted => [],
            DispatchState.None or
            DispatchState.Rejected or
            DispatchState.Revoked => [DispatchState.Requested],
            DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
            DispatchState.Accepted when trainStretch.CannotProceed => [DispatchState.Revoked],
            DispatchState.Accepted => [DispatchState.Revoked, DispatchState.Departed],
            _ => []
        };

        public TrainState[] NextTrainStates => trainStretch.Train.State switch
        {
            _ when trainStretch.IsLast && trainStretch.IsCompleted => [TrainState.Completed],
            TrainState.Planned => [TrainState.Manned, TrainState.Canceled],
            TrainState.Manned => [TrainState.Running, TrainState.Canceled],
            TrainState.Running => [TrainState.Aborted],
            _ => [],

        };

        public bool IsNext(DispatchState next) =>
            trainStretch.NextDispatchStates.Contains(next);

        public bool IsNext(TrainState next) =>
            trainStretch.NextTrainStates.Contains(next);

        public void SetDepartureTime() =>
            trainStretch.Departure.SetDepartureTime(trainStretch.TimeProvider.Time(trainStretch.Departure.Scheduled.DepartureTime));
        public void SetArrivalTime() =>
            trainStretch.Arrival.SetArrivalTime(trainStretch.TimeProvider.Time(trainStretch.Departure.Scheduled.ArrivalTime));
    }
}
