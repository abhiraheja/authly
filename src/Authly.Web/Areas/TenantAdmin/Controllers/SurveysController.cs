using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Policies;
using Authly.Modules.Surveys;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// TenantAdmin management of surveys: build questions, publish with enforcement + targeting, and
/// review aggregated responses. Gated on <c>survey.read</c> / <c>survey.manage</c>.
/// </summary>
[Route("tenantadmin/surveys")]
public sealed class SurveysController : TenantAdminControllerBase
{
    private static readonly string[] AuthMethodOptions =
        { AuthMethodCategories.Password, AuthMethodCategories.Social, AuthMethodCategories.Passkey, AuthMethodCategories.Phone, AuthMethodCategories.MagicLink };

    private readonly ISurveyService _surveys;
    private readonly IApplicationRepository _applications;
    private readonly ISocialProviderRepository _socialProviders;
    private readonly IRoleRepository _roles;
    private readonly IAudiencePreviewService _audience;

    public SurveysController(ISurveyService surveys, IApplicationRepository applications,
        ISocialProviderRepository socialProviders, IRoleRepository roles, IAudiencePreviewService audience,
        ITenantContext tenant) : base(tenant)
    {
        _surveys = surveys;
        _applications = applications;
        _socialProviders = socialProviders;
        _roles = roles;
        _audience = audience;
    }

    [RequireOperatorPermission("survey.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Surveys";
        return View(await _surveys.ListAsync(TenantId, ct));
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpGet("edit/{id:guid?}")]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = id is null ? "New survey" : "Edit survey";
        var vm = new SurveyEditViewModel();

        if (id is { } sid)
        {
            var survey = await _surveys.GetAsync(TenantId, sid, ct);
            if (survey is null) return NotFound();
            var t = PolicyTargetingJson.Parse(survey.Targeting);
            vm = new SurveyEditViewModel
            {
                Id = survey.Id,
                Title = survey.Title,
                Description = survey.Description,
                EnforcementMode = survey.EnforcementMode.ToString(),
                SkipDeadline = survey.SkipDeadline?.UtcDateTime,
                StartsAt = survey.StartsAt?.UtcDateTime,
                CloseDate = survey.CloseDate?.UtcDateTime,
                Audience = t.Audience,
                ApplicationIds = t.ApplicationIds,
                AuthMethods = t.AuthMethods,
                Providers = t.Providers,
                Roles = t.Roles,
                Match = t.Match,
                RandomizeQuestions = survey.RandomizeQuestions,
                Anonymous = survey.Anonymous,
                ShowProgressBar = survey.ShowProgressBar,
                ThankYouMessage = survey.ThankYouMessage,
                Status = survey.Status.ToString(),
                Questions = await _surveys.ListQuestionsAsync(TenantId, survey.Id, ct)
            };
        }

        await PopulateOptionsAsync(vm, ct);
        return View(vm);
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SurveyEditViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(vm, ct);
            if (vm.Id is { } eid) vm.Questions = await _surveys.ListQuestionsAsync(TenantId, eid, ct);
            return View(nameof(Edit), vm);
        }

        var input = ToInput(vm);
        if (vm.Id is { } id)
        {
            await _surveys.UpdateAsync(TenantId, id, input, CurrentAudit(), ct);
            TempData["Success"] = "Survey saved.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        var created = await _surveys.CreateAsync(TenantId, input, CurrentAudit(), ct);
        TempData["Success"] = "Draft created. Add questions, then publish.";
        return RedirectToAction(nameof(Edit), new { id = created.Id });
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/questions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddQuestion(Guid id, QuestionFormViewModel form, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(form.Title))
        {
            TempData["Error"] = "Question title is required.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        Enum.TryParse<SurveyQuestionType>(form.Type, out var type);
        var options = (form.Options ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        await _surveys.AddQuestionAsync(TenantId, id, new QuestionInput
        {
            Type = type, Title = form.Title, HelpText = form.HelpText, Required = form.Required,
            MediaUrl = form.MediaUrl, ScaleMin = form.ScaleMin, ScaleMax = form.ScaleMax,
            RandomizeOptions = form.RandomizeOptions, Placeholder = form.Placeholder, Options = options
        }, CurrentAudit(), ct);
        TempData["Success"] = "Question added.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/questions/{questionId:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteQuestion(Guid id, Guid questionId, CancellationToken ct)
    {
        await _surveys.DeleteQuestionAsync(TenantId, questionId, CurrentAudit(), ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/questions/{questionId:guid}/move")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveQuestion(Guid id, Guid questionId, int delta, CancellationToken ct)
    {
        await _surveys.MoveQuestionAsync(TenantId, questionId, delta, CurrentAudit(), ct);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        try
        {
            await _surveys.PublishAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Survey published. Targeted users will see it on next sign-in.";
        }
        catch (SurveyInvalidException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _surveys.ArchiveAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Survey archived.";
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("survey.manage")]
    [HttpPost("{id:guid}/re-request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReRequest(Guid id, CancellationToken ct)
    {
        await _surveys.ReRequestAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Re-requested — targeted users will be asked again on next sign-in.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("survey.read")]
    [HttpGet("{id:guid}/responses")]
    public async Task<IActionResult> Responses(Guid id, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(TenantId, id, ct);
        if (survey is null) return NotFound();
        ViewData["Title"] = "Survey responses";
        return View(new SurveyReportViewModel { Report = await _surveys.GetReportAsync(TenantId, id, ct) });
    }

    [RequireOperatorPermission("survey.read")]
    [HttpGet("{id:guid}/responses/export")]
    public async Task<IActionResult> ExportResponses(Guid id, CancellationToken ct)
    {
        var survey = await _surveys.GetAsync(TenantId, id, ct);
        if (survey is null) return NotFound();
        var csv = await _surveys.ExportResponsesCsvAsync(TenantId, id, ct);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return File(bytes, "text/csv", $"survey-{id}-responses.csv");
    }

    // --- helpers ---

    private SurveyEditInput ToInput(SurveyEditViewModel vm)
    {
        Enum.TryParse<PolicyEnforcementMode>(vm.EnforcementMode, out var mode);
        var targeting = new PolicyTargeting
        {
            Audience = NormalizeAudience(vm.Audience),
            ApplicationIds = vm.ApplicationIds ?? new(),
            AuthMethods = vm.AuthMethods ?? new(),
            Providers = vm.Providers ?? new(),
            Roles = vm.Roles ?? new(),
            Match = vm.Match == "all" ? "all" : "any"
        };
        return new SurveyEditInput
        {
            Title = vm.Title,
            Description = vm.Description,
            EnforcementMode = mode,
            SkipDeadline = ToUtc(vm.SkipDeadline),
            StartsAt = ToUtc(vm.StartsAt),
            CloseDate = ToUtc(vm.CloseDate),
            Targeting = targeting,
            RandomizeQuestions = vm.RandomizeQuestions,
            Anonymous = vm.Anonymous,
            ShowProgressBar = vm.ShowProgressBar,
            ThankYouMessage = vm.ThankYouMessage
        };
    }

    private static DateTimeOffset? ToUtc(DateTime? dt)
        => dt is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)) : null;

    private static string NormalizeAudience(string a) => a switch
    {
        Audiences.Applications or Audiences.AuthMethods or Audiences.Providers
            or Audiences.Roles or Audiences.Advanced => a,
        _ => Audiences.All
    };

    [RequireOperatorPermission("survey.read")]
    [HttpPost("preview-audience")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewAudience(AudiencePreviewForm form, CancellationToken ct)
    {
        var targeting = new PolicyTargeting
        {
            Audience = NormalizeAudience(form.Audience),
            ApplicationIds = form.ApplicationIds ?? new(),
            AuthMethods = form.AuthMethods ?? new(),
            Providers = form.Providers ?? new(),
            Roles = form.Roles ?? new(),
            Match = form.Match == "all" ? "all" : "any"
        };
        var preview = await _audience.PreviewAsync(TenantId, targeting, ct);
        return Json(new { count = preview.Count, sample = preview.SampleEmails, note = preview.Note });
    }

    private async Task PopulateOptionsAsync(SurveyEditViewModel vm, CancellationToken ct)
    {
        var apps = await _applications.ListByTenantAsync(TenantId, ct);
        vm.AvailableApps = apps.Select(a => new SurveyEditViewModel.AppOption(a.Id, a.Name)).ToList();
        var providers = await _socialProviders.ListByTenantAsync(TenantId, ct);
        vm.AvailableProviders = providers.Select(p => p.Provider).Distinct().ToList();
        vm.AvailableRoles = (await _roles.ListRolesAsync(TenantId, ct)).Select(r => r.Name).ToList();
        ViewData["AuthMethodOptions"] = AuthMethodOptions;
    }
}
