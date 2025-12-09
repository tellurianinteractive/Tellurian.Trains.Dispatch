using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Implements the action state machine that determines which actions are available
/// for a train section based on current state, dispatcher role, and business rules.
/// This replaces the mixed state/action pattern in the old DispatchState-based approach.
/// </summary>
internal class ActionStateMachine : IActionProvider
{
    public IEnumerable<ActionContext> GetAvailableActions(TrainSection section, IDispatcher dispatcher)
    {
        // Dispatch actions for departure dispatcher
        if (IsDepartureDispatcher(section, dispatcher))
        {
            foreach (var action in GetDepartureDispatcherActions(section, dispatcher))
                yield return action;
        }

        // Dispatch actions for arrival dispatcher
        if (IsArrivalDispatcher(section, dispatcher))
        {
            foreach (var action in GetArrivalDispatcherActions(section, dispatcher))
                yield return action;
        }

        // Pass actions for signal-controlled places this dispatcher controls
        foreach (var action in GetPassActions(section, dispatcher))
            yield return action;

        // Train actions (available to any dispatcher when conditions are met)
        foreach (var action in GetTrainActions(section, dispatcher))
            yield return action;

        // Clear action for canceled/aborted trains
        if (GetClearAction(section, dispatcher) is { } clearAction)
            yield return clearAction;
    }

    /// <inheritdoc/>
    public IEnumerable<ActionContext> GetAllAvailableActions(TrainSection section)
    {
        var departureDispatcher = section.From.Dispatcher;
        var arrivalDispatcher = section.To.Dispatcher;

        // Get actions from both dispatchers
        foreach (var action in GetAvailableActions(section, departureDispatcher))
            yield return action;

        // Avoid duplicates if same dispatcher controls both stations
        if (departureDispatcher.Id != arrivalDispatcher.Id)
        {
            foreach (var action in GetAvailableActions(section, arrivalDispatcher))
                yield return action;
        }
    }

    private static bool IsDepartureDispatcher(TrainSection section, IDispatcher dispatcher) =>
        section.From.Dispatcher.Id == dispatcher.Id;

    private static bool IsArrivalDispatcher(TrainSection section, IDispatcher dispatcher) =>
        section.To.Dispatcher.Id == dispatcher.Id;

    /// <summary>
    /// Gets actions available to the departure dispatcher.
    /// For non-first sections, dispatch actions are only available after Previous section has departed.
    /// </summary>
    private static IEnumerable<ActionContext> GetDepartureDispatcherActions(TrainSection section, IDispatcher dispatcher)
    {
        if (!section.Train.IsDispatchable) yield break;
        if (!section.IsPreviousDeparted) yield break;

        switch (section.State)
        {
            case DispatchState.None:
            case DispatchState.Rejected:
            case DispatchState.Revoked:
                yield return new ActionContext(section, DispatchAction.Request, dispatcher);
                break;

            case DispatchState.Requested:
                yield return new ActionContext(section, DispatchAction.Revoke, dispatcher);
                break;

            case DispatchState.Accepted:
                yield return new ActionContext(section, DispatchAction.Depart, dispatcher);
                yield return new ActionContext(section, DispatchAction.Revoke, dispatcher);
                break;
        }
    }

    /// <summary>
    /// Gets actions available to the arrival dispatcher.
    /// For non-first sections, dispatch actions are only available after Previous section has departed.
    /// </summary>
    private static IEnumerable<ActionContext> GetArrivalDispatcherActions(TrainSection section, IDispatcher dispatcher)
    {
        if (!section.Train.IsDispatchable) yield break;
        if (!section.IsPreviousDeparted) yield break;

        switch (section.State)
        {
            case DispatchState.Requested:
                yield return new ActionContext(section, DispatchAction.Accept, dispatcher);
                yield return new ActionContext(section, DispatchAction.Reject, dispatcher);
                break;

            case DispatchState.Departed:
                // Arrive is only available when on the last TrackStretch
                if (section.IsOnLastTrackStretch)
                {
                    yield return new ActionContext(section, DispatchAction.Arrive, dispatcher);
                }
                break;
        }
    }

    /// <summary>
    /// Gets Pass actions for signal-controlled places the dispatcher controls.
    /// Pass actions advance the train to the next TrackStretch
    /// when a SignalControlledPlace controlled by this dispatcher is the next control point.
    /// For non-first sections, actions are only available after Previous section has departed.
    /// </summary>
    private static IEnumerable<ActionContext> GetPassActions(TrainSection section, IDispatcher dispatcher)
    {
        if (!section.Train.IsDispatchable) yield break;
        if (!section.IsPreviousDeparted) yield break;
        if (section.State != DispatchState.Departed) yield break;
        if (section.IsOnLastTrackStretch) yield break;

        // Check if the next control point is a SignalControlledPlace controlled by this dispatcher
        var nextControlPoint = section.NextControlPoint;
        if (nextControlPoint is SignalControlledPlace scp && scp.ControlledBy.Id == dispatcher.Id)
        {
            yield return new ActionContext(section, DispatchAction.Pass, dispatcher, scp);
        }
    }

    /// <summary>
    /// Gets train state actions.
    /// First section only: Manned (when Planned), Canceled (when Planned), Running (when Manned).
    /// Non-first sections: Aborted (when Running) only.
    /// Note: Completed is NOT an action - it's automatically triggered when the last section arrives.
    /// </summary>
    private static IEnumerable<ActionContext> GetTrainActions(TrainSection section, IDispatcher dispatcher)
    {
        var trainState = section.Train.State;

        if (section.IsFirst)
        {
            // First section: Manned, Canceled (when Planned), Running (when Manned)
            switch (trainState)
            {
                case TrainState.Planned:
                    yield return new ActionContext(section, DispatchAction.Manned, dispatcher);
                    yield return new ActionContext(section, DispatchAction.Canceled, dispatcher);
                    break;

                case TrainState.Manned:
                    yield return new ActionContext(section, DispatchAction.Running, dispatcher);
                    break;
            }
        }
        else
        {
            // Non-first sections: Only Aborted is available when running
            if (trainState == TrainState.Running)
            {
                yield return new ActionContext(section, DispatchAction.Aborted, dispatcher);
            }
        }
    }

    /// <summary>
    /// Gets the Clear action if available for canceled/aborted trains on the stretch.
    /// </summary>
    private static ActionContext? GetClearAction(TrainSection section, IDispatcher dispatcher)
    {
        if (section.Train.IsCanceledOrAborted && section.State == DispatchState.Departed)
        {
            return new ActionContext(section, DispatchAction.Clear, dispatcher);
        }
        return null;
    }
}
