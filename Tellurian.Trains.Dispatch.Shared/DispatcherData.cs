namespace Tellurian.Trains.Dispatch.Shared;

/// <summary>
/// Represents a station dispatcher with links to detailed views.
/// Used in the /dispatchers list endpoint.
/// </summary>
public record DispatcherSummaryData
{
    /// <summary>
    /// Unique identifier for the dispatcher.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Station signature (e.g., "A", "Stc").
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Full station name.
    /// </summary>
    public required string StationName { get; init; }

    /// <summary>
    /// HATEOAS link to the dispatcher's detailed view.
    /// </summary>
    public required string Href { get; init; }
}

/// <summary>
/// Represents a station dispatcher with full details including arrivals and departures.
/// Used in the /dispatchers/{id} endpoint.
/// </summary>
public record DispatcherData
{
    /// <summary>
    /// Unique identifier for the dispatcher.
    /// </summary>
    public required int Id { get; init; }

    /// <summary>
    /// Station signature (e.g., "A", "Stc").
    /// </summary>
    public required string Signature { get; init; }

    /// <summary>
    /// Full station name.
    /// </summary>
    public required string StationName { get; init; }

    /// <summary>
    /// Preferred language for localization (e.g., "en", "sv", "de").
    /// </summary>
    public string? PreferredLanguage { get; init; }

    /// <summary>
    /// HATEOAS link to this resource.
    /// </summary>
    public required string Self { get; init; }

    /// <summary>
    /// HATEOAS link to the SSE events stream for this dispatcher.
    /// </summary>
    public required string Events { get; init; }

    /// <summary>
    /// Train sections arriving at this station.
    /// </summary>
    public required IReadOnlyList<TrainSectionData> Arrivals { get; init; }

    /// <summary>
    /// Train sections departing from this station.
    /// </summary>
    public required IReadOnlyList<TrainSectionData> Departures { get; init; }
}
