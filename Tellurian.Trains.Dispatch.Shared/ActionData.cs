namespace Tellurian.Trains.Dispatch.Shared;

/// <summary>
/// Represents a HATEOAS action link for executing dispatch or train actions.
/// Clients follow the href without constructing URLs themselves.
/// </summary>
public record ActionData(string Name, string DisplayName, string Href);
