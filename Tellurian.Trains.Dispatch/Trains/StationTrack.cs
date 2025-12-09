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

        public bool IsMainTrack { get; init; }
        public int DisplayOrder { get; init; }
        public int PlatformLength { get; set; }

        public override string ToString() => Number;
    };
}
