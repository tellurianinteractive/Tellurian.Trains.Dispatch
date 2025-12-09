using System.Data;
using Tellurian.Trains.Dispatch.Trains;

namespace Tellurian.Trains.Dispatch.Data.Access;

internal static class IDataRecordExtensions
{
    extension(IDataRecord record)
    {
        public OperationPlaceRecord ToOperationalPlaceData() => new()
        {
            Id = record.GetInt("Id"),
            Name = record.GetString("Name"),
            Signature = record.GetString("Signature"),
            IsJunction = record.GetBool("IsJunction"),
            IsManned = record.GetBool("IsManned"),
            ControlledByDispatcherId = record.GetIntOrNull("ControlledByOtherStationId"),
        };

        public StationTrackRecord ToStationTrackData() => new()
        {
            Id = record.GetInt("Id"),
            OperationPlaceId = record.GetInt("AtStationId"),
            TrackNumber = record.GetString("TrackNumber"),
            IsMainTrack = record.GetBool("IsMainTrack"),
            DisplayOrder = record.GetInt("DisplayOrder"),
            PlatformLength = record.GetInt("PlatformLength"),
        };

        public TrackStretchRecord ToTrackStretchData() => new()
        {
            Id = record.GetInt("Id"),
            StartOperationPlaceId = record.GetInt("FromStationId"),
            EndOperationPlaceId = record.GetInt("ToStationId"),
            NumberOfTracks = record.GetInt("NumberOfTracks"),
        };

        public DispatchStretchRecord ToDispatchStretchData() => new()
        {
            Id = record.GetInt("Id"),
            FromStationId = record.GetInt("ToStationId"),
            ToStationId = record.GetInt("ToStationId"),
        };

        public TrainRecord ToTrainData() => new()
        {
            Id = record.GetInt("TrainId"),
            Company = new(
                record.GetString("OperatorName"),
                record.GetString("OperatorName")),
            Identity = new(
                record.GetString("TrainCategoryPrefix"),
                record.GetInt("TrainNumber")),
        };

        public TrainStationCallRecord ToTrainStationCallData() => new()
        {
            Id = record.GetInt("CallId"),
            TrainId = record.GetInt("TrainId"),
            IsArrival = record.GetBool("IsArrival"),
            IsDeparture = record.GetBool("IsDeparture"),
            OperationPlaceId = record.GetInt("AtStationId"),
            StationTrackId = record.GetInt("AtStationTrackId"),
            Scheduled = new CallTime()
            {
                ArrivalTime = record.GetTime("ArrivalTime"),
                DepartureTime = record.GetTime("DepartureTime")
            },
        };


        public string GetString(string columnName) => record.GetString(record.GetOrdinal(columnName));
        public int GetInt(string columnName) => record.GetInt32(record.GetOrdinal(columnName));
        public int? GetIntOrNull(string columnName)
        {
            var ordinal = record.GetOrdinal(columnName);
            if (ordinal >= 0) return record.GetInt32(ordinal);
            return null;
        }

        public bool GetBool(string columnName) => record.GetBoolean(record.GetOrdinal(columnName));
        public TimeSpan GetTime(string columnName) => record.GetDateTime(record.GetOrdinal(columnName)).ToTime;

    }

    extension(DateTime time)
    {
        public TimeSpan ToTime => new(time.Hour, time.Minute, 0);
    }
}

