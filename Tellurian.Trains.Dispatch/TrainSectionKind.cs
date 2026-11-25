namespace Tellurian.Trains.Dispatch;

/// <summary>
/// How a <see cref="TrainSection"/> is cleared, which decides which dispatcher does what.
/// </summary>
/// <remarks>
/// Which trains are cleared where, and by whom, is the timetable model's rule (its dispatch legs). The kind only
/// says how the dispatchers act on it. See <see cref="TrainSection.FromStation"/> and <see cref="TrainSection.ToStation"/>.
/// </remarks>
public enum TrainSectionKind
{
    /// <summary>
    /// Between two dispatchers, each at a place the train departs from or arrives at. The departing side requests,
    /// departs and revokes; the arriving side accepts, rejects, revokes and marks the arrival.
    /// </summary>
    Normal,

    /// <summary>
    /// Between two dispatchers, where the train ends or leaves the line at a place along it without a dispatcher,
    /// such as an unmanned station. The dispatcher the train departs from requests, departs and marks when the train
    /// has arrived there; the dispatcher at the other end of the line accepts or rejects.
    /// </summary>
    Leaving,

    /// <summary>
    /// Between two dispatchers, where the train starts or joins the line at a place along it without a dispatcher.
    /// The dispatcher the train runs to requests, departs (gives the go) and marks the arrival; the dispatcher at the
    /// other end of the line accepts or rejects.
    /// </summary>
    Entering,

    /// <summary>
    /// Cleared by one dispatcher, for a train leaving towards a place they alone clear, such as a branch or an end
    /// where the driver works the points. When the dispatcher marks the train as departed, the section is done.
    /// </summary>
    OneDispatcherOutbound,

    /// <summary>
    /// Cleared by one dispatcher, for a train coming from a place they alone clear. The driver asks by phone, the
    /// dispatcher accepts (which is the go, and the train is on its way) or rejects, and marks the arrival.
    /// </summary>
    OneDispatcherInbound,
}
