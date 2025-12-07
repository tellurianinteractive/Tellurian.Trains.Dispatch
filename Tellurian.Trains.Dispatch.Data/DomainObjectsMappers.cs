using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data;

internal static class DomainObjectsMappers
{
    extension(OperationPlaceData data)
    {
        public OperationPlace Create() => data switch
        {
            _ when data.IsManned => new Station(data.Name, data.Signature)
            {
                Id = data.Id,
                IsManned = true,
                Tracks = data.Tracks
            },
            _ when data.ControlledByDispatcherId.HasValue => new SignalControlledPlace(data.Name, data.Signature, data.ControlledByDispatcherId.Value)
            {
                Id = data.Id,
                Tracks = data.Tracks
            },
            _ => new OtherPlace(data.Name, data.Signature)
            {
                Id = data.Id,
                Tracks = data.Tracks
            },
        };
    }
    extension(TrainData data)
    {
        public Train Create() => new(data.Company, data.Identity) { Id = data.Id, State = TrainState.Planned };
    }

    extension(TrainStationCallData data)
    {
        public TrainStationCall Create(Dictionary<int, OperationPlace> operationPlaces, Dictionary<int, Train> trains)
        {
            var operationPlace = operationPlaces[data.OperationPlaceId];
            var train = trains[data.TrainId];
            return new(train, operationPlace, data.Scheduled)
            {
                Id = data.Id,
                IsArrival = data.IsArrival,
                IsDeparture = data.IsDeparture,
                SequenceNumber = data.SequenceNumber
            };
        }
    }

    extension(TrackStretchData data)
    {
        public TrackStretch Create(Dictionary<int, OperationPlace> operationPlaces)
        {
            var from = operationPlaces[data.StartOperationPlaceId];
            var to = operationPlaces[data.EndOperationPlaceId];
            var tracks = new List<Track>();
            for (var i = 0; i < data.NumberOfTracks; i++)
            {
                tracks.Add(new((i + 1).ToString(), TrackOperationDirection.DoubleDirected));
            }
            return new(from, to, tracks) { Id = data.Id };
        }
    }

    extension(DispatchStretchData data)
    {
        public DispatchStretch Create(Dictionary<int, OperationPlace> operationPlaces, IEnumerable<TrackStretch> allTrackStretches)
        {
            if (operationPlaces[data.FromStationId] is not Station from || operationPlaces[data.ToStationId] is not Station to)
                throw new ArgumentException("Station from and/or to is missing", nameof(operationPlaces));
            return new DispatchStretch(from, to, allTrackStretches) { Id = data.Id };
        }
    }
}

