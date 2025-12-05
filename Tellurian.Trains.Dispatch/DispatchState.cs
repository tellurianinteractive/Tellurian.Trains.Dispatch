using Tellurian.Trains.Dispatch;

namespace Tellurian.Trains.Dispatch
{
    public enum DispatchState
    {
        None,
        Canceled,
        Requested,
        Rejected,
        Accepted,
        Revoked,
        Departed,
        Passed,
        Arrived,
    }


    public static class DispatchStateExtensions
    {
        extension(DispatchState state)
        {
            internal bool IsIn(DispatchState[] states) => states.Contains(state);
            /// <summary>
            /// Determines what possible next states that can be acted on at the arriving station.
            /// </summary>
            internal DispatchState[] NextArrivalStates => state switch {
                DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
                DispatchState.Departed => [DispatchState.Arrived],
                _ => [],
            };
            /// <summary>
            /// Determines what possible next states that can be acted on at the departing station.
            /// </summary>
            internal DispatchState[] NextDepartureStates => state switch {
                DispatchState.None or
                DispatchState.Rejected or
                DispatchState.Revoked => [DispatchState.Requested],
                DispatchState.Requested => [DispatchState.Revoked],
                DispatchState.Accepted => [DispatchState.Departed, DispatchState.Revoked],
                _ => [],
            };

            public string ActionResourceName => state switch
            {
                DispatchState.Requested => "Request",
                DispatchState.Rejected => "Reject",
                DispatchState.Accepted => "Accept",
                DispatchState.Revoked => "Revoke",
                DispatchState.Departed => "Departed",
                DispatchState.Arrived => "Arrived",
                _ => string.Empty,
            };

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
}
