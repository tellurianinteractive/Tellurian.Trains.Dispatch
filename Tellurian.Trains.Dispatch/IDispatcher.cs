namespace Tellurian.Trains.Dispatch;
/// <summary>
/// 
/// </summary>
public interface IDispatcher
{
    int Id { get; }
    string Name { get; }
    /// <summary>
    /// <see cref="TrainSection">Departing trains to dispatch.</see>
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainSection> Departures { get; }
    /// <summary>
    /// <see cref="TrainSection">Arriving trains to dispatch.</see>.
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainSection> Arrivals { get; }
}
