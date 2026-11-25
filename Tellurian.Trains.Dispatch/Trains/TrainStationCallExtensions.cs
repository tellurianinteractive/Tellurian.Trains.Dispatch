using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Trains;

public static class TrainStationCallExtensions
{
    extension(TrainStationCall call)
    {
        public void ChangeTrack(StationTrack other)
        {
            call.NewTrack = other;
        }

        public void SetDepartureTime(TimeSpan departureTime) =>
            call.Observed = new() { DepartureTime = departureTime };

        public void SetArrivalTime(TimeSpan arrivalTime) =>
            call.Observed = new() { DepartureTime = call.Observed.DepartureTime, ArrivalTime = arrivalTime };


    }
    extension(IEnumerable<TrainSectionDefinition> definitions)
    {
        internal IEnumerable<TrainSection> ToTrainSections(ITimeProvider timeProvider, ILogger logger)
        {
            foreach (var definition in definitions)
            {
                var section = TrainSection.Create(definition, timeProvider);
                if (section.HasValue)
                {
                    yield return section.Value!;
                }
                else if (logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning("Failed to create {ObjectName} with {Errors}", nameof(TrainSection), section.Error);
                }
            }
        }
    }

    extension(IEnumerable<TrainStationCall> calls)
    {

        internal IEnumerable<TrainSection> ToTrainSections(IEnumerable<DispatchStretch> dispatchStretches, ITimeProvider timeProvider, ILogger logger)
        {
            var stretches = dispatchStretches.ToList();
            var trains = calls.GroupBy(x => x.Train.Id);
            foreach (var train in trains)
            {
                var trainCalls = train.OrderBy(c => c.SequenceNumber).ToArray();
                for (int i = 0; i < trainCalls.Length - 1; i++)
                {
                    var section = stretches.CreateSection(trainCalls[i], trainCalls[i + 1], timeProvider);
                    if (section.HasValue)
                    {
                        yield return section.Value!;
                    }
                    else if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning("Failed to create {ObjectName} with {Errors}", nameof(TrainSection), section.Error);
                    }
                }
            }
        }
    }
}
