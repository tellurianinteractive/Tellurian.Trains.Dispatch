using Tellurian.Trains.Dispatch.Utilities;

namespace Tellurian.Trains.Dispatch.Tests;

[TestClass]
public sealed class OperatingDayTimeTests
{
    private static readonly TimeSpan DayStart = TimeSpan.FromHours(5);

    [TestMethod]
    public void TimeAfterDayStart_IsUnchanged() =>
        Assert.AreEqual(TimeSpan.FromHours(23.5), TimeSpan.FromHours(23.5).InOperatingDayStartingAt(DayStart));

    [TestMethod]
    public void DayStart_IsUnchanged() =>
        Assert.AreEqual(DayStart, DayStart.InOperatingDayStartingAt(DayStart));

    [TestMethod]
    public void TimeBeforeDayStart_IsAfterMidnight() =>
        Assert.AreEqual(TimeSpan.FromHours(24.5), TimeSpan.FromHours(0.5).InOperatingDayStartingAt(DayStart));

    [TestMethod]
    public void TimeOnNextDay_IsUnchanged() =>
        Assert.AreEqual(TimeSpan.FromHours(24.5), TimeSpan.FromHours(24.5).InOperatingDayStartingAt(DayStart));

    [TestMethod]
    public void DefaultDayStart_KeepsCalendarDay() =>
        Assert.AreEqual(TimeSpan.FromHours(0.5), TimeSpan.FromHours(0.5).InOperatingDayStartingAt(TimeSpan.Zero));
}
