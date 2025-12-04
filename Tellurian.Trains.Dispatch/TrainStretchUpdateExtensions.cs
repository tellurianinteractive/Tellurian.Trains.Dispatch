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
