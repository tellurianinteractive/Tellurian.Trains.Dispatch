
namespace Tellurian.Trains.Dispatch;
/// <summary>
/// 
/// </summary>
public interface IDispatcher
{
    string Name { get; }
    /// <summary>
    /// <see cref="TrainStretch">Departing trains to dispatch.</see>
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainStretch> Departures { get; }
    /// <summary>
    /// <see cref="TrainStretch">Arriving trains to dispatch.</see>.
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainStretch> Arrivals { get; }
}
