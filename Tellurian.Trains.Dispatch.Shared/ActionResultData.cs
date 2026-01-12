namespace Tellurian.Trains.Dispatch.Shared;

/// <summary>
/// Result of executing an action.
/// </summary>
public record ActionResultData
{
    /// <summary>
    /// Whether the action was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if the action failed, null otherwise.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The updated train section after the action, null if action failed.
    /// </summary>
    public TrainSectionData? TrainSection { get; init; }
}
