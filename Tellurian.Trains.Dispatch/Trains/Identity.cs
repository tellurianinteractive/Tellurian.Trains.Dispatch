namespace Tellurian.Trains.Dispatch.Trains;

public record Identity(string Prefix, int Number)
{
    public override string ToString() => $"{Prefix} {Number}";
};

