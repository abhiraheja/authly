using Authly.Core.Enums;

namespace Authly.Web.Areas.Portal.Models;

/// <summary>A row in the portal's survey response history.</summary>
public sealed record SurveyHistoryRow(string Title, SurveyResponseStatus Status, DateTimeOffset At);
