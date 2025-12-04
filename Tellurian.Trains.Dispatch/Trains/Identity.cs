using System;
using System.Collections.Generic;
using System.Text;

namespace Tellurian.Trains.Dispatch.Trains;

public record Identity(string Prefix, int Number)
{
    public override string ToString() => $"{Prefix} {Number}";
};

