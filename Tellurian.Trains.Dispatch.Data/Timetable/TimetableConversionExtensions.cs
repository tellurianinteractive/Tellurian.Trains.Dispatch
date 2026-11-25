using Tellurian.Trains.Dispatch.Layout;
using Tellurian.Trains.Dispatch.Trains;
using DispatchDispatchStretch = Tellurian.Trains.Dispatch.Layout.DispatchStretch;
using DispatchCompany = Tellurian.Trains.Dispatch.Trains.Company;
using DispatchStation = Tellurian.Trains.Dispatch.Trains.Station;
using DispatchStationTrack = Tellurian.Trains.Dispatch.Trains.StationTrack;
using DispatchTrackStretch = Tellurian.Trains.Dispatch.Layout.TrackStretch;
using DispatchTrain = Tellurian.Trains.Dispatch.Trains.Train;
using ModelDispatchStretch = Tellurian.Trains.Schedules.Model.Layouts.DispatchStretch;
using ModelOperationLocation = Tellurian.Trains.Schedules.Model.Layouts.OperationLocation;
using ModelSignalControlledLocation = Tellurian.Trains.Schedules.Model.Layouts.SignalControlledLocation;
using ModelStationCall = Tellurian.Trains.Schedules.Model.Timetables.StationCall;
using ModelStationTrack = Tellurian.Trains.Schedules.Model.Layouts.StationTrack;
using ModelTrackStretch = Tellurian.Trains.Schedules.Model.Layouts.TrackStretch;
using ModelTrain = Tellurian.Trains.Schedules.Model.Timetables.Train;
using Tellurian.Trains.Schedules.Model.Layouts;
using Tellurian.Trains.Schedules.Model.Timetables;

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
        /// <returns>
        /// A Station where a dispatcher is on duty (the model's <c>HasDispatcher</c>); a SignalControlledPlace for a
        /// signal-controlled location, controlled by the station that works it or by the driver; otherwise an OtherPlace.
        /// </returns>
        public OperationPlace ToOperationPlace()
        {
            var tracks = location.Tracks
                .Where(t => t.IsScheduled)
                .Select(t => t.ToDispatchStationTrack())
                .ToList();

            return location switch
            {
                _ when location.HasDispatcher =>
                    new DispatchStation(location.Name, location.Signature) { Id = location.Id, IsManned = true, Tracks = tracks },
                ModelSignalControlledLocation signal =>
                    new SignalControlledPlace(signal.Name, signal.Signature, signal.ControlledBy is { HasDispatcher: true } controller ? controller.Id : 0)
                    {
                        Id = signal.Id,
                        Tracks = tracks,
                        IsJunction = signal.IsJunction
                    },
                _ => new OtherPlace(location.Name, location.Signature) { Id = location.Id, Tracks = tracks }
            };
        }
    }

    extension(ModelStationTrack modelTrack)
    {
        /// <summary>
        /// Converts a model StationTrack to a Dispatch StationTrack.
        /// </summary>
        public DispatchStationTrack ToDispatchStationTrack() =>
            new(modelTrack.Designation)
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

            var modelCompany = modelTrain.EffectiveCompany;
            var company = new DispatchCompany(
                modelCompany?.Name ?? "Unknown",
                modelCompany?.Signature ?? "??");

            return new DispatchTrain(company, identity)
            {
                Id = 0, // Auto-generate
                MaxLength = modelTrain.Length.Meters is double meters ? (int)Math.Round(meters) : null
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
            if (!operationPlacesById.TryGetValue(modelCall.OperationLocation.Id, out var operationPlace))
            {
                return null;
            }

            // A place where trains only pass, such as a junction, may have no scheduled tracks.
            var track = operationPlace.Tracks.FirstOrDefault(t => t.Number == modelCall.Track.Designation)
                ?? operationPlace.Tracks.FirstOrDefault()
                ?? new DispatchStationTrack(modelCall.Track.Designation);

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
            Dictionary<int, DispatchStation> stationsByModelId,
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
