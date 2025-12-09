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
    extension(IEnumerable<TrainStationCall> calls)
    {

        internal IEnumerable<TrainSection> ToTrainSections(IEnumerable<DispatchStretch> dispatchStretches, ITimeProvider timeProvider, ILogger logger)
        {
            var trains = calls.GroupBy(x => x.Train.Id);
            foreach (var train in trains)
            {
                TrainSection? previousSection = null;
                var trainCalls = train.OrderBy(c => c.Train.Id).ThenBy(c => c.SequenceNumber).ToArray();
                for (int i = 0; i < trainCalls.Length; i++)
                {
                    if (i < trainCalls.Length - 1)
                    {
                        var dispatchStretch = dispatchStretches.FindFor(trainCalls[i], trainCalls[i + 1]);
                        var section = TrainSection.Create(dispatchStretch, trainCalls[i], trainCalls[i + 1], timeProvider);
                        if (section.HasValue)
                        {
                            section.Value!.Previous = previousSection;
                            previousSection = section.Value;
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
}
