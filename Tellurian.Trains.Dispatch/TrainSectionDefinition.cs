using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch;

/// <summary>
/// What an <see cref="Brokers.IBrokerDataProvider"/> says about one <see cref="TrainSection"/>: where the train runs
/// and which dispatchers clear it.
/// </summary>
/// <param name="Departure">The call the section starts at.</param>
/// <param name="Arrival">The call the section ends at.</param>
/// <param name="Kind">How the section is cleared.</param>
public sealed record TrainSectionDefinition(TrainStationCall Departure, TrainStationCall Arrival, TrainSectionKind Kind)
{
    /// <summary>
    /// The station whose dispatcher clears the train at the end it comes from; see <see cref="TrainSection.FromStation"/>.
    /// </summary>
    public Station? FromStation { get; init; }

    /// <summary>
    /// The station whose dispatcher clears the train at the end it goes to; see <see cref="TrainSection.ToStation"/>.
    /// </summary>
    public Station? ToStation { get; init; }

    /// <summary>
    /// The track stretches the train runs on, in the order of travel. Empty where they are not known.
    /// </summary>
    public IReadOnlyList<TrackStretch> TrackStretches { get; init; } = [];
}
