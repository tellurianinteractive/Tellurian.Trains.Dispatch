using System.Diagnostics;
using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using DispatchDispatchStretch = Tellurian.Trains.Dispatch.Layout.DispatchStretch;
using DispatchStationTrack = Tellurian.Trains.Dispatch.Trains.StationTrack;
using DispatchTrackStretch = Tellurian.Trains.Dispatch.Layout.TrackStretch;
using DispatchTrain = Tellurian.Trains.Dispatch.Trains.Train;
using ModelDispatchStretch = Tellurian.Trains.Schedules.Model.DispatchStretch;
using ModelOperationLocation = Tellurian.Trains.Schedules.Model.OperationLocation;
using ModelOtherLocation = Tellurian.Trains.Schedules.Model.OtherLocation;
using ModelSignalControlledLocation = Tellurian.Trains.Schedules.Model.SignalControlledLocation;
using ModelStation = Tellurian.Trains.Schedules.Model.Station;
using ModelStationCall = Tellurian.Trains.Schedules.Model.StationCall;
using ModelStationTrack = Tellurian.Trains.Schedules.Model.StationTrack;
using ModelTrackStretch = Tellurian.Trains.Schedules.Model.TrackStretch;
using ModelTrain = Tellurian.Trains.Schedules.Model.Train;

namespace Tellurian.Trains.Dispatch.Data.Timetable;

/// <summary>
/// Extension methods for converting Schedule model types to Dispatch domain types.
/// </summary>
public static class TimetableConversionExtensions
{
    extension(ModelOperationLocation location)
    {
        /// <summary>
        /// Converts an OperationLocation to the appropriate OperationPlace type.
        /// </summary>
        /// <returns>A Station if manned, otherwise an OtherPlace.</returns>
        public OperationPlace ToOperationPlace()
        {
            var tracks = location.Tracks
                .Where(t => t.IsScheduled)
                .Select(t => t.ToDispatchStationTrack())
                .ToList();

            return location switch
            {
                ModelStation station =>
                    new Station(station.Name, station.Signature) { Id = station.Id, IsManned = true, Tracks = tracks },
                ModelSignalControlledLocation signal =>
                    new SignalControlledPlace(signal.Name, signal.Signature, signal.ControlledBy?.Id ?? 0) { Id = signal.Id, Tracks = tracks },
                ModelOtherLocation other =>
                    new OtherPlace(other.Name, other.Signature) { Id = other.Id, Tracks = tracks },
                _ => throw new UnreachableException(location.GetType().Name)
            };
        }
    }

    extension(ModelStationTrack modelTrack)
    {
        /// <summary>
        /// Converts a model StationTrack to a Dispatch StationTrack.
        /// </summary>
        public DispatchStationTrack ToDispatchStationTrack() =>
            new(modelTrack.Number)
            {
                IsMainTrack = modelTrack.IsMain,
                DisplayOrder = modelTrack.DisplayOrder,
                MaxLength = modelTrack.Length > 0 ? (int)modelTrack.Length : null
            };
    }

    extension(ModelTrackStretch modelStretch)
    {
        /// <summary>
        /// Converts a model TrackStretch to a Dispatch TrackStretch.
        /// </summary>
        /// <param name="operationPlacesById">Dictionary of converted OperationPlaces by model station ID.</param>
        public DispatchTrackStretch? ToDispatchTrackStretch(Dictionary<int, OperationPlace> operationPlacesById)
        {
            if (!operationPlacesById.TryGetValue(modelStretch.StartId, out var start) ||
                !operationPlacesById.TryGetValue(modelStretch.EndId, out var end))
            {
                return null;
            }

            var tracks = modelStretch.TracksCount.ToDispatchTracks();

            return new DispatchTrackStretch(start, end, tracks)
            {
                Id = 0, // Auto-generate
                Length = modelStretch.Distance > 0 ? (int)(modelStretch.Distance * 1000) : null // km to meters
            };
        }
    }

    extension(int tracksCount)
    {
        /// <summary>
        /// Creates Dispatch Track objects based on the number of tracks.
        /// </summary>
        public List<Track> ToDispatchTracks()
        {
            if (tracksCount <= 1)
            {
                return [new Track("1", TrackOperationDirection.DoubleDirected)];
            }

            var tracks = new List<Track>();
            for (int i = 0; i < tracksCount; i++)
            {
                var isUpTrack = i % 2 == 0;
                tracks.Add(new Track((i + 1).ToString(), TrackOperationDirection.DoubleDirected)
                {
                    IsUpTrack = isUpTrack
                });
            }
            return tracks;
        }
    }

    extension(ModelTrain modelTrain)
    {
        /// <summary>
        /// Converts a model Train to a Dispatch Train.
        /// </summary>
        public DispatchTrain ToDispatchTrain()
        {
            var identity = new Identity(
                modelTrain.Category?.Prefix ?? "",
                modelTrain.Number);

            var company = new Company(
                modelTrain.Company?.Name ?? "Unknown",
                modelTrain.Company?.Signature ?? "??");

            return new DispatchTrain(company, identity)
            {
                Id = 0, // Auto-generate
                MaxLength = modelTrain.Length.Meters
            };
        }
    }

    extension(ModelStationCall modelCall)
    {
        /// <summary>
        /// Converts a model StationCall to a Dispatch TrainStationCall.
        /// </summary>
        /// <param name="dispatchTrain">The converted Dispatch Train.</param>
        /// <param name="operationPlacesById">Dictionary of converted OperationPlaces by model station ID.</param>
        /// <param name="sequenceNumber">The sequence number of this call in the train's journey.</param>
        public TrainStationCall? ToTrainStationCall(
            DispatchTrain dispatchTrain,
            Dictionary<int, OperationPlace> operationPlacesById,
            int sequenceNumber)
        {
            if (!operationPlacesById.TryGetValue(modelCall.Station.Id, out var operationPlace))
            {
                return null;
            }

            var track = operationPlace.Tracks.FirstOrDefault(t => t.Number == modelCall.Track.Number)
                ?? operationPlace.Tracks.FirstOrDefault();

            if (track is null) return null;

            var callTime = new CallTime
            {
                ArrivalTime = modelCall.Arrival.Value,
                DepartureTime = modelCall.Departure.Value
            };

            return new TrainStationCall(dispatchTrain, operationPlace, callTime)
            {
                Id = 0, // Auto-generate
                PlannedTrack = track,
                IsArrival = modelCall.IsArrival,
                IsDeparture = modelCall.IsDeparture,
                SequenceNumber = sequenceNumber
            };
        }
    }

    extension(ModelDispatchStretch modelDispatchStretch)
    {
        /// <summary>
        /// Converts a model DispatchStretch to a Dispatch DispatchStretch.
        /// </summary>
        /// <param name="stationsByModelId">Dictionary of converted Stations by model station ID.</param>
        /// <param name="allTrackStretches">All converted TrackStretches for path finding.</param>
        public DispatchDispatchStretch? ToDispatchStretch(
            Dictionary<int, Station> stationsByModelId,
            IEnumerable<DispatchTrackStretch> allTrackStretches)
        {
            if (!stationsByModelId.TryGetValue(modelDispatchStretch.From.Id, out var fromStation) ||
                !stationsByModelId.TryGetValue(modelDispatchStretch.To.Id, out var toStation))
            {
                return null;
            }

            return new DispatchDispatchStretch(fromStation, toStation, allTrackStretches)
            {
                Id = modelDispatchStretch.Id
            };
        }
    }
}
