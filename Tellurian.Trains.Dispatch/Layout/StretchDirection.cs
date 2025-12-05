namespace Tellurian.Trains.Dispatch.Layout;

public enum StretchDirection
{
    Forward,
    Reverse,
}

public static class StretchDirectionExtensions
{
    extension(StretchDirection direction)
    {
        public bool IsForward => direction == StretchDirection.Forward; 
        public bool IsReverse => direction == StretchDirection.Reverse;
    }
}
