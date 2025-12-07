namespace Tellurian.Trains.Dispatch;
/// <summary>
/// 
/// </summary>
public interface IDispatcher
{
    int Id { get; }
    string Name { get; }
    string Signature { get; }
    /// <summary>
    /// <see cref="TrainSection">Departing trains to dispatch.</see>
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainSection> GetDepartures(int maxCount = 10);
    /// <summary>
    /// <see cref="TrainSection">Arriving trains to dispatch.</see>.
    /// </summary>
    /// <remarks>
    /// This property need to be called after each arrival action performed by the <see cref="IDispatcher"/>.
    /// </remarks>
    IEnumerable<TrainSection> GetArrivals(int maxCount = 10);
}
