namespace Tellurian.Trains.Dispatch.Trains
{
    public record StationTrack(string Number)
    {
        public override string ToString() => Number;
    };
}
