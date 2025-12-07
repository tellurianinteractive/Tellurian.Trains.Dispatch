using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Trains
{
    public record Train(Company Company, Identity Identity)
    {
        public int Id { get; set { field = value.OrNextId; } }

        public TrainState State { get; set { if (!isRevoke) Previous = field; field = value; } }
        private TrainState? Previous = null;

        /// <summary>
        /// Maximum train length in meters.
        /// Used for meeting constraints - trains can only meet where both fit.
        /// Null means no constraint.
        /// </summary>
        public int? MaxLength { get; init; }

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

            public IList<TrainStationCall> Calls(IEnumerable<TrainStationCall> allCalls) =>
                [.. allCalls
                .Where(c => c.Train.Id == train.Id)
                .OrderBy(c => c.SequenceNumber)];
        }
    }
}
