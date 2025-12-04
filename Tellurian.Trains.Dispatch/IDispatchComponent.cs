namespace Tellurian.Trains.Dispatch;

public interface IDispatchComponent
{
    Task InitAsync(bool isRestart, CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);

}
