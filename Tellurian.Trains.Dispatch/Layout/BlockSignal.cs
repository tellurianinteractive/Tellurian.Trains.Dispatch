using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Layout;
/// <summary>
/// A <see cref="BlockSignal"/> is a way to divide a <see cref="DispatchStretch"/> into
/// sections, where each section can have one <see cref="TrainSection">train</see>.
/// </summary>
/// <param name="Name">Name of place associated with the <see cref="BlockSignal"/></param>
/// <param name="ControlledBy">The <see cref="IDispatcher"/> that controls this <see cref="BlockSignal"/></param>
/// <param name="IsJunction">True if there is at least one diverging track.</param>
/// <param name="SequenceNumber"></param>
/// <remarks>
/// A <see cref="BlockSignal"/> is assumed to exist in both directions on a <see cref="DispatchStretch"/>.
/// A <see cref="DispatchStretch"/> can have several <see cref="BlockSignal">block signals</see>
/// and they are in the order of the <see cref="SequenceNumber"/>
/// and a <see cref="TrainSection"/> must be passed in that order.
/// </remarks>
public record BlockSignal(string Name, IDispatcher ControlledBy, bool IsJunction = false)
{
    public int Id { get; set { field = value.OrNextId; } }
    /// <summary>
    /// The dispatcher that controls this block signal.
    /// Initially set to a placeholder during configuration, resolved to actual StationDispatcher during Broker initialization.
    /// </summary>
    public IDispatcher ControlledBy { get; set; } = ControlledBy;
    public override string ToString() =>
        IsJunction ? $"{Name}, junction controlled by {ControlledBy.Name}" : $"{Name} controlled by {ControlledBy.Name}";
}
