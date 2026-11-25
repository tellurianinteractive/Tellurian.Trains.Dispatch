using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Implements the action state machine that determines which actions are available
/// for a train section based on current state, dispatcher role, and business rules.
/// </summary>
/// <remarks>
/// The roles (who requests, accepts, departs and marks the arrival) depend on the
/// <see cref="TrainSectionKind"/> of the section, see <see cref="TrainSection.RequestingDispatcher"/> and the others.
/// </remarks>
internal sealed class ActionStateMachine : IActionProvider
{
    public IEnumerable<ActionContext> GetAvailableActions(TrainSection section, IDispatcher dispatcher) =>
        GetActions(section, dispatcher).DistinctBy(a => (a.Action, a.PassTarget?.Id));

    /// <inheritdoc/>
    public IEnumerable<ActionContext> GetAllAvailableActions(TrainSection section) =>
        section.Dispatchers.SelectMany(dispatcher => GetAvailableActions(section, dispatcher));

    private static IEnumerable<ActionContext> GetActions(TrainSection section, IDispatcher dispatcher)
    {
        foreach (var action in GetDispatchActions(section, dispatcher))
            yield return action;

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

    private static bool Is(IDispatcher? role, IDispatcher dispatcher) => role is not null && role.Id == dispatcher.Id;

    /// <summary>
    /// Gets the dispatch actions available to the dispatcher in its roles for the section.
    /// For non-first sections, dispatch actions are only available after Previous section has departed.
    /// </summary>
    private static IEnumerable<ActionContext> GetDispatchActions(TrainSection section, IDispatcher dispatcher)
    {
        if (!section.Train.IsDispatchable) yield break;
        if (!section.IsPreviousDeparted) yield break;

        var isRequesting = Is(section.RequestingDispatcher, dispatcher);
        var isAccepting = Is(section.AcceptingDispatcher, dispatcher);
        var isDeparting = Is(section.DepartingDispatcher, dispatcher);
        var isArriving = Is(section.ArrivingDispatcher, dispatcher);

        switch (section.State)
        {
            case DispatchState.None:
            case DispatchState.Rejected:
            case DispatchState.Revoked:
                if (isRequesting)
                    yield return new ActionContext(section, DispatchAction.Request, dispatcher);
                if (section.Kind == TrainSectionKind.OneDispatcherInbound && isAccepting)
                {
                    // The driver asks by phone; accepting is the go.
                    yield return new ActionContext(section, DispatchAction.Accept, dispatcher);
                    if (section.State != DispatchState.Rejected)
                        yield return new ActionContext(section, DispatchAction.Reject, dispatcher);
                }
                if (section.Kind == TrainSectionKind.OneDispatcherOutbound && isDeparting)
                    yield return new ActionContext(section, DispatchAction.Depart, dispatcher);
                break;

            case DispatchState.Requested:
                if (isAccepting)
                {
                    yield return new ActionContext(section, DispatchAction.Accept, dispatcher);
                    yield return new ActionContext(section, DispatchAction.Reject, dispatcher);
                }
                if (isRequesting)
                    yield return new ActionContext(section, DispatchAction.Revoke, dispatcher);
                break;

            case DispatchState.Accepted:
                if (isDeparting)
                    yield return new ActionContext(section, DispatchAction.Depart, dispatcher);
                // Both the requesting and the accepting dispatcher can revoke before the train departs.
                if (isRequesting || isAccepting)
                    yield return new ActionContext(section, DispatchAction.Revoke, dispatcher);
                break;

            case DispatchState.Departed:
                // Arrive is only available when on the last TrackStretch
                if (isArriving && section.IsOnLastTrackStretch)
                    yield return new ActionContext(section, DispatchAction.Arrive, dispatcher);
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
        if (nextControlPoint is SignalControlledPlace scp && scp.ControlledBy?.Id == dispatcher.Id)
        {
            yield return new ActionContext(section, DispatchAction.Pass, dispatcher, scp);
        }
    }

    /// <summary>
    /// Gets train state actions.
    /// First section only: Manned (when Planned), Canceled (when Planned).
    /// Non-first sections: Aborted (when Running) only.
    /// UndoTrainState is available when the train has a previous state to revert to.
    /// Note: Completed is NOT an action - it's automatically triggered when the last section arrives.
    /// </summary>
    private static IEnumerable<ActionContext> GetTrainActions(TrainSection section, IDispatcher dispatcher)
    {
        var trainState = section.Train.State;

        if (section.IsFirst)
        {
            // First section: Manned (when Planned), Canceled (when Planned or Manned)
            // Note: Running is set implicitly when the train departs
            switch (trainState)
            {
                case TrainState.Planned:
                    yield return new ActionContext(section, DispatchAction.AssignDriver, dispatcher);
                    yield return new ActionContext(section, DispatchAction.Cancel, dispatcher);
                    break;

                case TrainState.Manned:
                    yield return new ActionContext(section, DispatchAction.Cancel, dispatcher);
                    break;
            }
        }
        else
        {
            // Non-first sections: Only Aborted is available when running
            if (trainState == TrainState.Running)
            {
                yield return new ActionContext(section, DispatchAction.Abort, dispatcher);
            }
        }

        // UndoTrainState is available only for Manned, Canceled, and Aborted states
        if (section.Train.State is TrainState.Manned or TrainState.Canceled or TrainState.Aborted
            && section.Train.PreviousState is not null)
        {
            yield return new ActionContext(section, DispatchAction.UndoTrainState, dispatcher);
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
