namespace Tellurian.Trains.Dispatch.Layout;

/// <summary>
/// Defines the operational direction(s) a track can be used for.
/// </summary>
public enum TrackOperationDirection
{
    /// <summary>
    /// Track can only be used in the From→To direction of the TrackStretch.
    /// </summary>
    ForwardOnly,

    /// <summary>
    /// Track can only be used in the To→From direction of the TrackStretch.
    /// </summary>
    BackwardOnly,

    /// <summary>
    /// Track can be used in either direction (Swedish model, single-track meets).
    /// </summary>
    DoubleDirected,

    /// <summary>
    /// Track is out of service.
    /// </summary>
    Closed
}

public static class TrackOperationDirectionExtensions
{
    extension(TrackOperationDirection direction)
    {
        public bool IsForwardOnly => direction == TrackOperationDirection.ForwardOnly;
        public bool IsBackwardOnly => direction == TrackOperationDirection.BackwardOnly;
        public bool IsDoubleDirected => direction == TrackOperationDirection.DoubleDirected;
        public bool IsClosed => direction == TrackOperationDirection.Closed;

        /// <summary>
        /// Returns true if the track can be used in the forward direction (From→To).
        /// </summary>
        public bool AllowsForward => direction is TrackOperationDirection.ForwardOnly or TrackOperationDirection.DoubleDirected;

        /// <summary>
        /// Returns true if the track can be used in the backward direction (To→From).
        /// </summary>
        public bool AllowsBackward => direction is TrackOperationDirection.BackwardOnly or TrackOperationDirection.DoubleDirected;
    }
}
