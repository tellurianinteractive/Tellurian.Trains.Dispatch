namespace Tellurian.Trains.Dispatch.Utilities
{
    public static class UtilityExtensions
    {

        private static int _nextId;

        extension(int id)
        {
            internal int OrNextId => id <= 0 ? Interlocked.Increment(ref _nextId) : id;
        }

        extension(TimeSpan time)
        {
            /// <summary>
            /// The time within an operating day that starts at <paramref name="dayStart"/>:
            /// a time of day before the start belongs to the end of the operating day, after midnight.
            /// </summary>
            /// <param name="dayStart">The fast clock time the operating day starts at.</param>
            public TimeSpan InOperatingDayStartingAt(TimeSpan dayStart) =>
                time < dayStart ? time + TimeSpan.FromDays(1) : time;
        }

        extension<T>(IList<T>? list)
        {
            public IList<T>? Reversed => list is null ? null : [.. list.Reverse()];
        }
    }
}
