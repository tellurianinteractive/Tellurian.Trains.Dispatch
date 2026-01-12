namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Actions that can be performed on a <see cref="TrainSection"/> by dispatchers or operators.
/// This enum separates the concept of "actions" from "states" (<see cref="DispatchState"/>).
/// </summary>
public enum DispatchAction
{
    // Dispatch actions (performed by dispatchers)

    /// <summary>
    /// Departure dispatcher requests permission to send train.
    /// </summary>
    Request,

    /// <summary>
    /// Arrival dispatcher accepts the request.
    /// </summary>
    Accept,

    /// <summary>
    /// Arrival dispatcher rejects the request.
    /// </summary>
    Reject,

    /// <summary>
    /// Departure dispatcher revokes an accepted request.
    /// </summary>
    Revoke,

    /// <summary>
    /// Departure dispatcher marks the train as departed.
    /// </summary>
    Depart,

    /// <summary>
    /// Dispatcher marks a train as having passed a signal-controlled place.
    /// The target place is specified separately in the action context.
    /// </summary>
    Pass,

    /// <summary>
    /// Arrival dispatcher marks the train as arrived.
    /// </summary>
    Arrive,

    /// <summary>
    /// Clear a canceled/aborted train from the stretch.
    /// </summary>
    Clear,

    // Train actions (performed by train operator/crew)

    /// <summary>
    /// Assign a driver/crew to the train (train becomes manned).
    /// </summary>
    AssignDriver,

    /// <summary>
    /// Start the train's journey (train begins running).
    /// </summary>
    Start,

    /// <summary>
    /// Cancel the train before it starts running.
    /// </summary>
    Cancel,

    /// <summary>
    /// Abort train operation while running.
    /// </summary>
    Abort,

    /// <summary>
    /// Complete the train's entire journey.
    /// </summary>
    Complete,

    /// <summary>
    /// Undo the last train state change, reverting to the previous state.
    /// This allows dispatchers to correct mistakes (e.g., accidentally canceling or aborting a train).
    /// </summary>
    UndoTrainState
}
