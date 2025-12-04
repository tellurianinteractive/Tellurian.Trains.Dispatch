namespace Tellurian.Trains.Dispatch.Trains;

public enum TrainState
{
    Planned,
    Manned,
    Running,
    Canceled,
    Aborted,
    Completed,
    Undo,
}

public static class TrainStateExtensions
{
    extension(TrainState state)
    {
        public bool IsNot(TrainState[] otherStates) => !otherStates.Contains(state);
    }
}

