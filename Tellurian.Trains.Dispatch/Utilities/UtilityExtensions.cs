namespace Tellurian.Trains.Dispatch.Utilities
{
    public static class UtilityExtensions
    {

        private static int _nextId;

        extension(int id)
        {
            internal int OrNextId => id <= 0 ? Interlocked.Increment(ref _nextId) : id;
        }

        extension<T>(IList<T>? list)
        {
            public IList<T>? Reversed => list is null ? null : [.. list.Reverse()];
        }
    }
}
