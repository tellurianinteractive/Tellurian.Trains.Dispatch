namespace Tellurian.Trains.Dispatch.Brokers;
/// <summary>
/// Saves and read <see cref="Broker"/> state.
/// </summary>
/// <remarks>
/// Implementations of this interface must repect these conditions:
/// - Only one operation <see cref="SaveDispatchCallsAsync(IEnumerable{TrainSection}, CancellationToken)"/> or
///   <see cref="ReadTrainSections(CancellationToken)"/> at a time.
/// - If a <see cref="SaveDispatchCallsAsync(IEnumerable{TrainSection}, CancellationToken)"/> already is ongoing when
///   this function is called again, it should return immediately without saving.
/// </remarks>
public interface IBrokerStateProvider
{
    Task<bool> SaveDispatchCallsAsync(IEnumerable<TrainSection> dispatchCalls, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainSection>> ReadTrainSections(CancellationToken cancellationToken = default);
}

