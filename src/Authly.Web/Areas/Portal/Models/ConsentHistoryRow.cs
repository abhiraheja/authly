using Authly.Core.Enums;

namespace Authly.Web.Areas.Portal.Models;

/// <summary>A row in the portal's policy-decision history.</summary>
public sealed record ConsentHistoryRow(string Title, PolicyDecisionType Decision, int Version, DateTimeOffset DecidedAt);
