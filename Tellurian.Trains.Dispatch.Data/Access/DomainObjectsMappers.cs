using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;

internal static class DomainObjectsMappers
{
    extension(OperationPlaceRecord record)
    {
        public OperationPlace Map() => record switch
        {
            _ when record.IsManned => new Station(record.Name, record.Signature)
            {
                Id = record.Id,
                IsManned = true,
                Tracks = record.Tracks
            },
            _ when record.ControlledByDispatcherId.HasValue => new SignalControlledPlace(record.Name, record.Signature, record.ControlledByDispatcherId.Value)
            {
                Id = record.Id,
                Tracks = record.Tracks
            },
            _ => new OtherPlace(record.Name, record.Signature)
            {
                Id = record.Id,
                Tracks = record.Tracks
            },
        };
    }

    extension(StationTrackRecord record)
    {
        public StationTrack Map() => new(record.TrackNumber)
        {
            IsMainTrack = record.IsMainTrack,
            DisplayOrder = record.DisplayOrder,
            PlatformLength = record.PlatformLength
        };
    }

    extension(TrainRecord record)
    {
        public Train Map() => new(record.Company, record.Identity) { Id = record.Id, State = TrainState.Planned };
    }

    extension(TrainStationCallRecord data)
    {
        public TrainStationCall Map(Dictionary<int, OperationPlace> operationPlaces, Dictionary<int, Train> trains)
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

    extension(TrackStretchRecord record)
    {
        public TrackStretch Map(Dictionary<int, OperationPlace> operationPlaces)
        {
            var from = operationPlaces[record.StartOperationPlaceId];
            var to = operationPlaces[record.EndOperationPlaceId];
            var tracks = new List<Track>();
            for (var i = 0; i < record.NumberOfTracks; i++)
            {
                tracks.Add(new((i + 1).ToString(), TrackOperationDirection.DoubleDirected));
            }
            return new(from, to, tracks) { Id = record.Id };
        }
    }

    extension(DispatchStretchRecord record)
    {
        public DispatchStretch Map(Dictionary<int, OperationPlace> operationPlaces, IEnumerable<TrackStretch> allTrackStretches)
        {
            if (operationPlaces[record.FromStationId] is not Station from || operationPlaces[record.ToStationId] is not Station to)
                throw new ArgumentException("Station from and/or to is missing", nameof(operationPlaces));
            return new DispatchStretch(from, to, allTrackStretches) { Id = record.Id };
        }
    }
}

