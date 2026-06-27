using System.Security.Claims;
using Authly.Core.Enums;
using Authly.Modules.Common;
using Authly.Modules.Surveys;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// The survey runner an end-user is redirected to (by <c>RequiredPromptsGateMiddleware</c>) when a
/// targeted survey is pending. Renders the questions, accepts the submission/skip/decline, then
/// resumes the original destination (the gate re-evaluates and shows the next pending item, if any).
/// </summary>
[Authorize(Policy = AuthPolicies.User)]
[Route("account/survey")]
public sealed class SurveyController : Controller
{
    private readonly ISurveyService _surveys;

    public SurveyController(ISurveyService surveys) => _surveys = surveys;

    private Guid TenantId => Guid.Parse(User.FindFirstValue(UserClaims.TenantId)!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirstValue(UserClaims.SessionId), out var id) ? id : null;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Run(Guid id, string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SafeReturnUrl(returnUrl);
        var model = await _surveys.GetForRunnerAsync(TenantId, id, ct);
        if (model is null || model.Survey.Status != PolicyStatus.Published)
            return Redirect(safeReturn ?? Portal());

        ViewData["Title"] = model.Survey.Title;
        ViewData["ReturnUrl"] = safeReturn;
        return View(model);
    }

    [HttpPost("{id:guid}/submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(Guid id, string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SafeReturnUrl(returnUrl);
        var model = await _surveys.GetForRunnerAsync(TenantId, id, ct);
        if (model is null) return Redirect(safeReturn ?? Portal());

        var answers = BuildAnswers(model);
        try
        {
            await _surveys.SubmitAsync(TenantId, id, UserId, CurrentSessionId, answers, CurrentAudit(), ct);
        }
        catch (SurveyInvalidException ex)
        {
            TempData["Error"] = ex.Message;
            ViewData["Title"] = model.Survey.Title;
            ViewData["ReturnUrl"] = safeReturn;
            return View(nameof(Run), model);
        }
        return Redirect(safeReturn ?? Portal());
    }

    [HttpPost("{id:guid}/skip")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Skip(Guid id, string? returnUrl, CancellationToken ct)
    {
        await _surveys.SkipAsync(TenantId, id, UserId, CurrentSessionId, CurrentAudit(), ct);
        return Redirect(SafeReturnUrl(returnUrl) ?? Portal());
    }

    [HttpPost("{id:guid}/decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(Guid id, string? returnUrl, CancellationToken ct)
    {
        await _surveys.DeclineAsync(TenantId, id, UserId, CurrentSessionId, CurrentAudit(), ct);
        return Redirect(SafeReturnUrl(returnUrl) ?? Portal());
    }

    /// <summary>Reads the posted form (fields named <c>q_{questionId}</c>) into typed answer inputs.</summary>
    private List<SurveyAnswerInput> BuildAnswers(SurveyForRunner model)
    {
        var answers = new List<SurveyAnswerInput>();
        foreach (var qo in model.Questions)
        {
            var q = qo.Question;
            var field = $"q_{q.Id}";
            var values = Request.Form[field];
            if (values.Count == 0) continue;

            var input = new SurveyAnswerInput { QuestionId = q.Id };
            switch (q.Type)
            {
                case SurveyQuestionType.Number or SurveyQuestionType.Rating:
                    if (double.TryParse(values[0], out var n)) input.Number = n;
                    break;
                case SurveyQuestionType.SingleChoice or SurveyQuestionType.Dropdown:
                    if (Guid.TryParse(values[0], out var one)) input.OptionIds.Add(one);
                    break;
                case SurveyQuestionType.MultipleChoice:
                    foreach (var v in values)
                        if (Guid.TryParse(v, out var g)) input.OptionIds.Add(g);
                    break;
                default: // ShortText, LongText, YesNo, Date
                    input.Text = values[0];
                    break;
            }

            var empty = input.Text is null && input.Number is null && input.OptionIds.Count == 0;
            if (!empty) answers.Add(input);
        }
        return answers;
    }

    private static string Portal() => "/portal/profile";

    private string? SafeReturnUrl(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;

    private AuditContext CurrentAudit() => new(
        UserId, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
