namespace Tellurian.Trains.Dispatch.Trains
{
    /// <summary>
    /// A specific track/platform at a station or signal-controlled place.
    /// </summary>
    /// <param name="Number">Track designation (e.g., "1", "2a", "5").</param>
    public record StationTrack(string Number)
    {
        /// <summary>
        /// Maximum train length in meters that this track can accommodate.
        /// Used for meeting constraints - trains can only use tracks they fit on.
        /// Null means no constraint.
        /// </summary>
        public int? MaxLength { get; init; }

        /// <summary>
        /// Indicates whether this is a main track (through track) or a side track.
        /// </summary>
        public bool IsMainTrack { get; init; }

        /// <summary>
        /// Display order for sorting tracks in the web API.
        /// Defaults to track number when possible, otherwise 0.
        /// </summary>
        public int DisplayOrder { get; init; }

        /// <summary>
        /// Length of the platform in meters.
        /// A value greater than 0 indicates that passengers can board/alight at this track.
        /// Null means no platform or unknown length.
        /// </summary>
        public int? PlatformLength { get; init; }

        public override string ToString() => Number;
    };
}
