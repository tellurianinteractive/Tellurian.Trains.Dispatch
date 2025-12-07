namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Represents the current state of a train section's dispatch process.
/// </summary>
public enum DispatchState
{
    /// <summary>Not yet started.</summary>
    None,
    /// <summary>Departure requested by departure dispatcher.</summary>
    Requested,
    /// <summary>Request accepted by arrival dispatcher.</summary>
    Accepted,
    /// <summary>Request rejected by arrival dispatcher.</summary>
    Rejected,
    /// <summary>Accepted request revoked by departure dispatcher.</summary>
    Revoked,
    /// <summary>Train has departed and is on the stretch.</summary>
    Departed,
    /// <summary>Train has arrived at destination.</summary>
    Arrived,
    /// <summary>Train section canceled or aborted.</summary>
    Canceled,
}

public static class DispatchStateExtensions
{
    extension(DispatchState state)
    {
        internal bool IsIn(DispatchState[] states) => states.Contains(state);

        public string StateResourceName => state.ToString();

        public string BackgroundColor => state switch
        {
            DispatchState.Requested => "Orange",
            DispatchState.Accepted or
            DispatchState.Arrived => "Green",
            _ => "Red",
        };
    }
}
