namespace Tellurian.Trains.Dispatch;

/// <summary>
/// Provides available actions for train sections based on current state and dispatcher role.
/// This interface abstracts the action determination logic, replacing the mixed
/// state/action pattern in the old DispatchState-based approach.
/// </summary>
public interface IActionProvider
{
    /// <summary>
    /// Gets all available actions for a train section that can be performed by the specified dispatcher.
    /// </summary>
    /// <param name="section">The train section to get actions for.</param>
    /// <param name="dispatcher">The dispatcher requesting available actions.</param>
    /// <returns>A collection of action contexts representing available actions.</returns>
    IEnumerable<ActionContext> GetAvailableActions(TrainSection section, IDispatcher dispatcher);

    /// <summary>
    /// Gets all available actions for a train section regardless of dispatcher.
    /// Useful for administrative views or testing.
    /// </summary>
    /// <param name="section">The train section to get actions for.</param>
    /// <returns>A collection of action contexts representing all available actions.</returns>
    IEnumerable<ActionContext> GetAllAvailableActions(TrainSection section);
}
