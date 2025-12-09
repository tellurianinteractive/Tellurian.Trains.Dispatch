namespace Tellurian.Trains.Dispatch.Data.Access;

internal class TrackStretchRecord
{
    public int Id { get; init; }
    public int StartOperationPlaceId { get; init; }
    public int EndOperationPlaceId { get; init; }
    public int NumberOfTracks { get; init; }
}

