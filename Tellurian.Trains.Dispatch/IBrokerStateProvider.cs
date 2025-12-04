namespace Tellurian.Trains.Dispatch;
/// <summary>
/// Saves and read <see cref="Broker"/> state.
/// </summary>
/// <remarks>
/// Implementations of this interface must repect these conditions:
/// - Only one operation <see cref="SaveDispatchCallsAsync(IEnumerable{TrainStretch}, CancellationToken)"/> or
///   <see cref="ReadDispatchCallsAsync(CancellationToken)"/> at a time.
/// - If a <see cref="SaveDispatchCallsAsync(IEnumerable{TrainStretch}, CancellationToken)"/> already is ongoing when
///   this function is called again, it should return immediately without saving.
/// </remarks>
public interface IBrokerStateProvider
{ 
    Task<bool> SaveDispatchCallsAsync(IEnumerable<TrainStretch> dispatchCalls, CancellationToken cancellationToken = default);
    Task<IEnumerable<TrainStretch>> ReadDispatchCallsAsync(CancellationToken cancellationToken = default);
}

