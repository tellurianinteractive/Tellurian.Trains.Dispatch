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

        /// <summary>
        /// Returns true if the departure dispatcher can mark the next block signal as passed.
        /// The next block signal must be controlled by the departure dispatcher.
        /// </summary>
        public bool DepartureDispatcherCanPassNextBlockSignal =>
            trainSection.NextBlockSignalIsControlledBy(trainSection.From.Dispatcher);

        /// <summary>
        /// Returns true if the arrival dispatcher can mark the next block signal as passed.
        /// This requires that:
        /// 1. The next block signal is controlled by the arrival dispatcher
        /// 2. All previous block signals NOT controlled by the arrival dispatcher have been passed.
        /// </summary>
        public bool ArrivalDispatcherCanPassNextBlockSignal =>
            trainSection.NextBlockSignalIsControlledBy(trainSection.To.Dispatcher) &&
            trainSection.AllPreviousNonArrivalControlledSignalsPassed();

        /// <summary>
        /// Checks if the next block signal (at CurrentBlockIndex) is controlled by the specified dispatcher.
        /// </summary>
        private bool NextBlockSignalIsControlledBy(IDispatcher dispatcher)
        {
            var currentIndex = trainSection.CurrentBlockIndex;
            if (currentIndex >= trainSection.BlockSignalPassages.Count) return false;
            var nextPassage = trainSection.BlockSignalPassages[currentIndex];
            return nextPassage.IsExpected && nextPassage.BlockSignal.ControlledBy.Id == dispatcher.Id;
        }

        /// <summary>
        /// Checks if all block signals before the current one that are NOT controlled
        /// by the arrival dispatcher have been passed.
        /// </summary>
        private bool AllPreviousNonArrivalControlledSignalsPassed()
        {
            var arrivalDispatcher = trainSection.To.Dispatcher;
            var currentIndex = trainSection.CurrentBlockIndex;

            for (int i = 0; i < currentIndex && i < trainSection.BlockSignalPassages.Count; i++)
            {
                var passage = trainSection.BlockSignalPassages[i];
                if (passage.BlockSignal.ControlledBy.Id != arrivalDispatcher.Id && !passage.IsPassed)
                    return false;
            }
            return true;
        }
        public bool HasRemaningBlockSignalsToPass() =>
            trainSection.BlockSignalPassages.Any(bsp => bsp.IsExpected);

        public DispatchState[] NextDispatchStates => trainSection.State switch
        {
            _ when trainSection.Train.IsCanceledOrAborted => [],
            DispatchState.None or
            DispatchState.Rejected or
            DispatchState.Revoked => [DispatchState.Requested],
            DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected, DispatchState.Revoked],
            DispatchState.Accepted when trainSection.CannotProceed => [DispatchState.Revoked],
            DispatchState.Accepted => [DispatchState.Revoked, DispatchState.Departed],
            DispatchState.Departed when trainSection.HasRemaningBlockSignalsToPass() => [DispatchState.Passed],
            DispatchState.Departed when trainSection.HasPassedAllBlockSignals => [DispatchState.Arrived],
            _ => []
        };

        /// <summary>
        /// States available for the arrival dispatcher.
        /// Arrived is only available after all block signals have been passed.
        /// The arrival dispatcher can only mark a block signal as passed when all previous
        /// block signals not controlled by them have been passed.
        /// </summary>
        public DispatchState[] ArrivalStates => trainSection.State switch
        {
            _ when trainSection.Train.IsCanceledOrAborted => [],
            DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
            DispatchState.Departed when trainSection.ArrivalDispatcherCanPassNextBlockSignal => [DispatchState.Passed],
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
            DispatchState.Departed when trainSection.DepartureDispatcherCanPassNextBlockSignal => [DispatchState.Passed],
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
            trainSection.Arrival.SetArrivalTime(trainSection.TimeProvider.Time(trainSection.Arrival.Scheduled.ArrivalTime));
    }
}
