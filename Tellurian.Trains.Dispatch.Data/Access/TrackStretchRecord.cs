namespace Tellurian.Trains.Dispatch.Data.Access;

internal class TrackStretchRecord
{
    public int Id { get; init; }
    public int StartOperationPlaceId { get; init; }
    public int EndOperationPlaceId { get; init; }
    public int NumberOfTracks { get; init; }

    /// <summary>
    /// Length of the track stretch in meters.
    /// </summary>
    public int? Length { get; init; }

    /// <summary>
    /// CSS class name for styling this track stretch in the UI.
    /// </summary>
    public string? CssClass { get; init; }
}

