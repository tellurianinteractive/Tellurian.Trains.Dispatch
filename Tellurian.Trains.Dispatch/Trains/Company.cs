namespace Tellurian.Trains.Dispatch.Trains;

public record Company(string Name, string Signture)
{
    public override string ToString() => Signture;
}

