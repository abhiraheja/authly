namespace Authly.Modules.Common;

/// <summary>
/// Transport-agnostic details about the originating request, captured at the controller
/// boundary so module services can record login history / sessions without depending on
/// HTTP types.
/// </summary>
public sealed record RequestInfo(
    string? IpAddress = null,
    string? UserAgent = null,
    string? Device = null)
{
    public static readonly RequestInfo Unknown = new();
}
