namespace Tellurian.Trains.Dispatch.Trains;

public enum TrainState
{
    Planned,
    Manned,
    Running,
    Canceled,
    Aborted,
    Completed,
}

public static class TrainStateExtensions
{
    extension(TrainState state)
    {
        internal bool IsIn(TrainState[] states) => states.Contains(state);
        public bool IsNot(TrainState[] otherStates) => !otherStates.Contains(state);
    }
}

