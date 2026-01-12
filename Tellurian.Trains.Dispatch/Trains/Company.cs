namespace Tellurian.Trains.Dispatch.Trains;

public record Company(string Name, string Signature)
{
    public override string ToString() => Signature;
}

