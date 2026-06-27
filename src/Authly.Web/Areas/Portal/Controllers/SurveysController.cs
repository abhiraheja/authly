using Authly.Modules.Surveys;
using Authly.Web.Areas.Portal.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: the signed-in user's survey response history.</summary>
[Route("portal/surveys")]
public sealed class SurveysController : PortalControllerBase
{
    private readonly ISurveyService _surveys;

    public SurveysController(ISurveyService surveys) => _surveys = surveys;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Surveys";

        var responses = await _surveys.ListUserResponsesAsync(TenantId, UserId, ct);
        var titles = (await _surveys.ListAsync(TenantId, ct)).ToDictionary(s => s.Id, s => s.Title);

        var rows = responses
            .Select(r => new SurveyHistoryRow(
                titles.TryGetValue(r.SurveyId, out var t) ? t : "(removed survey)",
                r.Status, r.SubmittedAt ?? r.StartedAt))
            .ToList();

        return View(rows);
    }
}
