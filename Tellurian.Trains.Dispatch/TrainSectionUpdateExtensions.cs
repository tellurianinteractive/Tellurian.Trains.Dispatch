using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

internal static class TrainSectionUpdateExtensions
{
    extension(TrainSection trainSection)
    {
        internal bool SetManned()
        {
            return TryUpdateState(trainSection, TrainState.Manned);
        }
        internal bool SetRunning()
        {
            return TryUpdateState(trainSection, TrainState.Running, condition: trainSection.IsFirst);
        }
        internal bool SetCanceled()
        {
            return TryUpdateState(trainSection, TrainState.Canceled);
        }
        internal bool SetAborted()
        {
            return TryUpdateState(trainSection, TrainState.Aborted);
        }
        internal bool TrySetCompleted()
        {
            return TryUpdateState(trainSection, TrainState.Completed, condition: trainSection.IsLast && trainSection.IsCompleted);
        }

        private bool TryUpdateState(TrainState next, bool condition = true)
        {
            if (condition && trainSection.IsNext(next))
            {
                if (trainSection.Train.IsCanceledOrAborted) trainSection.State = DispatchState.Canceled;
                else trainSection.Train.State = next;
                return true;
            }
            return false;
        }

        internal bool Request()
        {
            if (TryUpdateState(trainSection, DispatchState.Requested))
            {
                trainSection.SetManned();
                return true;
            }
            return false;
        }

        internal bool Accept()
        {
            return TryUpdateState(trainSection, DispatchState.Accepted);
        }

        internal bool Reject()
        {
            return TryUpdateState(trainSection, DispatchState.Rejected);
        }

        internal bool Revoke()
        {
            return TryUpdateState(trainSection, DispatchState.Revoked);
        }

        internal bool Departed()
        {
            if (TryUpdateState(trainSection, DispatchState.Departed))
            {
                trainSection.SetDepartureTime();
                trainSection.SetRunning();
                return true;
            }
            return false;

        }

        internal bool Arrived()
        {
            if (TryUpdateState(trainSection, DispatchState.Arrived))
            {
                trainSection.SetArrivalTime();
                trainSection.TrySetCompleted();
                return true;

            }
            return false;
        }

        /// <summary>
        /// Marks the block signal at the specified index as passed.
        /// Must be called in sequence - trains pass block signals in order.
        /// </summary>
        /// <param name="blockSignalIndex">The index of the block signal (0-based).</param>
        /// <returns>True if the passage was marked, false if not allowed.</returns>
        internal bool PassBlockSignal(int blockSignalIndex)
        {
            if (trainSection.State != DispatchState.Departed) return false;
            if (blockSignalIndex != trainSection.CurrentBlockIndex) return false;
            var passage = trainSection.BlockSignalPassages[blockSignalIndex];
            if (!passage.IsExpected) return false;
            passage.State = BlockSignalPassageState.Passed;
            return true;
        }

        /// <summary>
        /// Manually clears an aborted or canceled train from the stretch.
        /// This frees the block the train was occupying.
        /// </summary>
        /// <returns>True if the train was cleared, false if not applicable.</returns>
        internal bool ClearFromStretch()
        {
            if (!trainSection.Train.IsCanceledOrAborted) return false;
            if (trainSection.State != DispatchState.Departed) return false;
            trainSection.DispatchStretch.ActiveTrains.Remove(trainSection);
            foreach (var passage in trainSection.BlockSignalPassages.Where(p => p.IsExpected))
            {
                passage.State = BlockSignalPassageState.Canceled;
            }
            trainSection.State = DispatchState.Canceled;
            return true;
        }

        private bool TryUpdateState(DispatchState next, bool condition = true)
        {
            if (condition && trainSection.IsNext(next))
            {
                if (trainSection.Train.IsCanceledOrAborted) trainSection.State = DispatchState.Canceled;
                else trainSection.State = next;
                return true;
            }
            return false;
        }
    }
}
