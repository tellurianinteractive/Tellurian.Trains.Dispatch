using Tellurian.Trains.Dispatch.Layout;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Represents a dispatchable action with all context needed to execute it.
/// This provides a unified model for all dispatch actions, replacing the
/// separate handling of dispatch states, train states, and block signal passages.
/// </summary>
/// <param name="Section">The train section this action applies to.</param>
/// <param name="Action">The type of action to perform.</param>
/// <param name="Dispatcher">The dispatcher authorized to perform this action.</param>
/// <param name="PassTarget">For Pass actions, the signal-controlled place being passed.</param>
public record ActionContext(
    TrainSection Section,
    DispatchAction Action,
    IDispatcher Dispatcher,
    SignalControlledPlace? PassTarget = null)
{
    /// <summary>
    /// Display name for the action, suitable for UI.
    /// </summary>
    public string DisplayName => Action switch
    {
        DispatchAction.Pass when PassTarget is not null => $"Pass {PassTarget.Name}",
        DispatchAction.UndoTrainState => $"Undo {Section.Train.State}",
        _ => Action.ToString()
    };

    /// <summary>
    /// Returns true if this is a dispatch action (performed by dispatchers).
    /// </summary>
    public bool IsDispatchAction => Action is
        DispatchAction.Request or
        DispatchAction.Accept or
        DispatchAction.Reject or
        DispatchAction.Revoke or
        DispatchAction.Depart or
        DispatchAction.Pass or
        DispatchAction.Arrive or
        DispatchAction.Clear;

    /// <summary>
    /// Returns true if this is a train action (performed by train crew/operator).
    /// </summary>
    public bool IsTrainAction => Action is
        DispatchAction.Manned or
        DispatchAction.Running or
        DispatchAction.Canceled or
        DispatchAction.Aborted or
        DispatchAction.Completed or
        DispatchAction.UndoTrainState;

    /// <summary>
    /// Returns true if this action is for the departure side of the dispatch.
    /// </summary>
    public bool IsDepartureAction => Action is
        DispatchAction.Request or
        DispatchAction.Revoke or
        DispatchAction.Depart;

    /// <summary>
    /// Returns true if this action is for the arrival side of the dispatch.
    /// </summary>
    public bool IsArrivalAction => Action is
        DispatchAction.Accept or
        DispatchAction.Reject or
        DispatchAction.Arrive;
}
