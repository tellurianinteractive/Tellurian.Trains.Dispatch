using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains
{
    public record Train(Company Company, Identity Identity)
    {
        public int Id { get; set { field = value.OrNextId; } }
        public TrainState State { get; set { if (!isRevoke) Previous = field; field = value; } }
        private TrainState? Previous = null;
        public IList<TrainStretch> Sections { get; } = [];
        public void RevokeLastStateChange()
        {
            if (Previous is null) return;
            isRevoke = true;
            State = Previous.Value;
            Previous = null;
            isRevoke = false;
        }
        private bool isRevoke = false;
    }

    public static class TrainExtensions
    {
        extension(Train train)
        {
            public bool IsCanceled => train.State == TrainState.Canceled;
            public bool IsAborted => train.State == TrainState.Aborted;
            public bool IsCanceledOrAborted => train.IsCanceled || train.IsAborted;
            public bool IsNotCancelledOrAborted => train.State.IsNot([TrainState.Canceled, TrainState.Aborted]);

        }
    }
}
