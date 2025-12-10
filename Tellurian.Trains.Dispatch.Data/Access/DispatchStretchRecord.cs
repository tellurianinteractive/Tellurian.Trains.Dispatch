namespace Tellurian.Trains.Dispatch.Data.Access;

internal class DispatchStretchRecord
{
    public int Id { get; init; }
    public int FromStationId { get; init; }
    public int ToStationId { get; init; }

    /// <summary>
    /// CSS class name for styling this dispatch stretch in the UI.
    /// </summary>
    public string? CssClass { get; init; }
}

