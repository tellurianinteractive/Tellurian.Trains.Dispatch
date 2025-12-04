using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch;

internal static class TrainStretchUpdateExtensions
{
    extension(TrainStretch trainStretch)
    {
        internal bool SetManned()
        {
            return TryUpdateState(trainStretch, TrainState.Manned);
        }
        internal bool SetRunning()
        {
            return TryUpdateState(trainStretch, TrainState.Running, condition: trainStretch.IsFirst);
        }
        internal bool SetCanceled()
        {
            return TryUpdateState(trainStretch, TrainState.Canceled);
        }
        internal bool SetAborted()
        {
            return TryUpdateState(trainStretch, TrainState.Aborted);
        }
        internal bool TrySetCompleted()
        {
            return TryUpdateState(trainStretch, TrainState.Completed, condition: trainStretch.IsLast && trainStretch.IsCompleted);
        }

        private bool TryUpdateState(TrainState next, bool condition = true)
        {
            if (condition && trainStretch.IsNext(next))
            {
                if (trainStretch.Train.IsCanceledOrAborted) trainStretch.State = DispatchState.Canceled;
                else trainStretch.Train.State = next;
                return true;
            }
            return false;
        }

        internal bool Request()
        {
            if (TryUpdateState(trainStretch, DispatchState.Requested))
            {
                trainStretch.SetManned();
                return true;
            }
            return false;
        }

        internal bool Accept()
        {
            return TryUpdateState(trainStretch, DispatchState.Accepted);
        }

        internal bool Reject()
        {
            return TryUpdateState(trainStretch, DispatchState.Rejected);
        }

        internal bool Revoke()
        {
            return TryUpdateState(trainStretch, DispatchState.Revoked);
        }

        internal bool Departed()
        {
            if (TryUpdateState(trainStretch, DispatchState.Departed))
            {
                trainStretch.SetDepartureTime();
                trainStretch.SetRunning();
                return true;
            }
            return false;

        }

        internal bool Arrived()
        {
            if (TryUpdateState(trainStretch, DispatchState.Arrived))
            {
                trainStretch.SetArrivalTime();
                trainStretch.TrySetCompleted();
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
            if (trainStretch.State != DispatchState.Departed) return false;
            if (blockSignalIndex != trainStretch.CurrentBlockIndex) return false;
            var passage = trainStretch.BlockSignalPassages[blockSignalIndex];
            if (!passage.IsExpected) return false;
            passage.State = BlockPassageState.Passed;
            return true;
        }

        /// <summary>
        /// Manually clears an aborted or canceled train from the stretch.
        /// This frees the block the train was occupying.
        /// </summary>
        /// <returns>True if the train was cleared, false if not applicable.</returns>
        internal bool ClearFromStretch()
        {
            if (!trainStretch.Train.IsCanceledOrAborted)                return false;
            if (trainStretch.State != DispatchState.Departed)                return false;
            trainStretch.DispatchStretch.ActiveTrains.Remove(trainStretch);
            foreach (var passage in trainStretch.BlockSignalPassages.Where(p => p.IsExpected))
            {
                passage.State = BlockPassageState.Canceled;
            }
            trainStretch.State = DispatchState.Canceled;
            return true;
        }

        private bool TryUpdateState(DispatchState next, bool condition = true)
        {
            if (condition && trainStretch.IsNext(next))
            {
                if (trainStretch.Train.IsCanceledOrAborted) trainStretch.State = DispatchState.Canceled;
                else trainStretch.State = next;
                return true;
            }
            return false;
        }
    }
}
