using Microsoft.Extensions.Logging;
using Tellurian.Trains.Dispatch;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Trains;

public static class TrainStationCallExtensions
{
    extension(TrainStationCall call)
    {
        public void ChangeTrack(StationTrack other)
        {
            call.Track = other;
        }

        public void SetDepartureTime(TimeSpan departureTime) =>
            call.Observed = new() { DepartureTime = departureTime };

        public void SetArrivalTime(TimeSpan arrivalTime) =>
            call.Observed = new() { DepartureTime = call.Observed.DepartureTime, ArrivalTime = arrivalTime };


    }
    extension(IEnumerable<TrainStationCall> calls)
    {

        internal IEnumerable<TrainStretch> ToDispatchSections(IEnumerable<DispatchStretch> dispatchStretches, ITimeProvider timeProvider, ILogger logger)
        {
            var trains = calls.GroupBy(x => x.Train.Id);
            foreach (var train in trains)
            {
                var trainCalls = train.OrderBy(c => c.Scheduled.DepartureTime).ToArray();
                for (int i = 0; i < trainCalls.Length; i++)
                {
                    trainCalls[i].SequenceNumner = i + 1;
                    if (i == 0)
                    {
                        trainCalls[i].IsArrival = false;
                    }
                    else if (i == trainCalls.Length - 1)
                    {
                        trainCalls[i].IsDeparture = false;
                    }
                    if (i < trainCalls.Length - 1)
                    {
                        var dispatchStretch = dispatchStretches.FindFor(trainCalls[i], trainCalls[i + 1]);
                        var section = TrainStretch.Create(dispatchStretch, trainCalls[i], trainCalls[i + 1], timeProvider);
                        {
                            ;
                            if (section.HasValue) yield return section.Value!;
                            if (logger.IsEnabled(LogLevel.Warning))
                                logger.LogWarning("Failed to create {ObjectName} with {E 0rrors}", nameof(TrainStretch), section.Error);

                        }
                    }
                }
            }
        }
    }
}
