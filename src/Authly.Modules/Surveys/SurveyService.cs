using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Policies;

namespace Authly.Modules.Surveys;

/// <summary>Admin authoring + sign-in delivery + reporting for the surveys engine.</summary>
public interface ISurveyService
{
    // Admin
    Task<IReadOnlyList<Survey>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<Survey?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuestionWithOptions>> ListQuestionsAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default);
    Task<Survey> CreateAsync(Guid tenantId, SurveyEditInput input, AuditContext actor, CancellationToken ct = default);
    Task UpdateAsync(Guid tenantId, Guid id, SurveyEditInput input, AuditContext actor, CancellationToken ct = default);
    Task PublishAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
    Task ArchiveAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
    Task ReRequestAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    // Question builder
    Task AddQuestionAsync(Guid tenantId, Guid surveyId, QuestionInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteQuestionAsync(Guid tenantId, Guid questionId, AuditContext actor, CancellationToken ct = default);
    Task MoveQuestionAsync(Guid tenantId, Guid questionId, int delta, AuditContext actor, CancellationToken ct = default);

    // Gate + runner
    Task<IReadOnlyList<PendingSurvey>> GetPendingAsync(Guid tenantId, Guid userId, Guid? sessionId, Guid? applicationId, CancellationToken ct = default);
    Task<SurveyForRunner?> GetForRunnerAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default);
    Task SubmitAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId, IReadOnlyList<SurveyAnswerInput> answers, AuditContext actor, CancellationToken ct = default);
    Task SkipAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId, AuditContext actor, CancellationToken ct = default);
    Task DeclineAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId, AuditContext actor, CancellationToken ct = default);

    // Reporting + portal
    Task<SurveyReport> GetReportAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default);
    Task<IReadOnlyList<SurveyResponse>> ListUserResponsesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class SurveyService : ISurveyService
{
    private readonly ISurveyRepository _surveys;
    private readonly ILoginHistoryRepository _logins;
    private readonly ISocialIdentityRepository _social;
    private readonly IAuditLogger _audit;

    public SurveyService(ISurveyRepository surveys, ILoginHistoryRepository logins,
        ISocialIdentityRepository social, IAuditLogger audit)
    {
        _surveys = surveys;
        _logins = logins;
        _social = social;
        _audit = audit;
    }

    // --- Admin ---

    public Task<IReadOnlyList<Survey>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _surveys.ListByTenantAsync(tenantId, ct);

    public Task<Survey?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _surveys.GetAsync(tenantId, id, ct);

    public async Task<IReadOnlyList<QuestionWithOptions>> ListQuestionsAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
    {
        var questions = await _surveys.ListQuestionsAsync(surveyId, ct);
        var options = (await _surveys.ListOptionsBySurveyAsync(surveyId, ct)).GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SurveyQuestionOption>)g.OrderBy(o => o.Order).ToList());
        return questions
            .Select(q => new QuestionWithOptions(q, options.TryGetValue(q.Id, out var o) ? o : Array.Empty<SurveyQuestionOption>()))
            .ToList();
    }

    public async Task<Survey> CreateAsync(Guid tenantId, SurveyEditInput input, AuditContext actor, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var survey = new Survey
        {
            TenantId = tenantId,
            Title = input.Title.Trim(),
            Description = Normalize(input.Description),
            Status = PolicyStatus.Draft,
            EnforcementMode = input.EnforcementMode,
            SkipDeadline = input.SkipDeadline,
            StartsAt = input.StartsAt,
            CloseDate = input.CloseDate,
            Targeting = PolicyTargetingJson.Serialize(input.Targeting),
            RandomizeQuestions = input.RandomizeQuestions,
            Anonymous = input.Anonymous,
            ShowProgressBar = input.ShowProgressBar,
            ThankYouMessage = Normalize(input.ThankYouMessage),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _surveys.AddAsync(survey, ct);
        await _audit.LogAsync("survey.created", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id,
            metadata: new { survey.Title }, ct: ct);
        return survey;
    }

    public async Task UpdateAsync(Guid tenantId, Guid id, SurveyEditInput input, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, id, ct);
        survey.Title = input.Title.Trim();
        survey.Description = Normalize(input.Description);
        survey.EnforcementMode = input.EnforcementMode;
        survey.SkipDeadline = input.SkipDeadline;
        survey.StartsAt = input.StartsAt;
        survey.CloseDate = input.CloseDate;
        survey.Targeting = PolicyTargetingJson.Serialize(input.Targeting);
        survey.RandomizeQuestions = input.RandomizeQuestions;
        survey.Anonymous = input.Anonymous;
        survey.ShowProgressBar = input.ShowProgressBar;
        survey.ThankYouMessage = Normalize(input.ThankYouMessage);
        survey.UpdatedAt = DateTimeOffset.UtcNow;
        await _surveys.UpdateAsync(survey, ct);
        await _audit.LogAsync("survey.updated", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id, ct: ct);
    }

    public async Task PublishAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, id, ct);
        var questions = await _surveys.ListQuestionsAsync(id, ct);
        if (questions.Count == 0)
            throw new SurveyInvalidException("Add at least one question before publishing.");

        survey.Status = PolicyStatus.Published;
        survey.PublishedAt = DateTimeOffset.UtcNow;
        survey.UpdatedAt = DateTimeOffset.UtcNow;
        await _surveys.UpdateAsync(survey, ct);
        await _audit.LogAsync("survey.published", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id,
            metadata: new { questions = questions.Count, mode = survey.EnforcementMode.ToString() }, ct: ct);
    }

    public async Task ArchiveAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, id, ct);
        survey.Status = PolicyStatus.Archived;
        survey.UpdatedAt = DateTimeOffset.UtcNow;
        await _surveys.UpdateAsync(survey, ct);
        await _audit.LogAsync("survey.archived", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id, ct: ct);
    }

    public async Task ReRequestAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, id, ct);
        survey.ConsentResetAt = DateTimeOffset.UtcNow;
        survey.UpdatedAt = DateTimeOffset.UtcNow;
        await _surveys.UpdateAsync(survey, ct);
        await _audit.LogAsync("survey.re_requested", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id, ct: ct);
    }

    // --- Question builder ---

    public async Task AddQuestionAsync(Guid tenantId, Guid surveyId, QuestionInput input, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, surveyId, ct);
        var existing = await _surveys.ListQuestionsAsync(surveyId, ct);
        var question = new SurveyQuestion
        {
            SurveyId = survey.Id,
            TenantId = tenantId,
            Order = existing.Count,
            Type = input.Type,
            Title = input.Title.Trim(),
            HelpText = Normalize(input.HelpText),
            Required = input.Required,
            MediaUrl = Normalize(input.MediaUrl),
            ScaleMin = input.ScaleMin,
            ScaleMax = input.ScaleMax,
            RandomizeOptions = input.RandomizeOptions,
            Placeholder = Normalize(input.Placeholder)
        };
        await _surveys.AddQuestionAsync(question, ct);

        if (RequiresOptions(input.Type))
        {
            var order = 0;
            foreach (var label in input.Options.Select(o => o.Trim()).Where(o => o.Length > 0))
            {
                await _surveys.AddOptionAsync(new SurveyQuestionOption
                {
                    QuestionId = question.Id, SurveyId = survey.Id, TenantId = tenantId, Order = order++, Label = label
                }, ct);
            }
        }

        await _audit.LogAsync("survey.question_added", actor, tenantId: tenantId, resourceType: "survey", resourceId: survey.Id, ct: ct);
    }

    public async Task DeleteQuestionAsync(Guid tenantId, Guid questionId, AuditContext actor, CancellationToken ct = default)
    {
        var question = await _surveys.GetQuestionAsync(tenantId, questionId, ct) ?? throw new KeyNotFoundException("Question not found.");
        await _surveys.DeleteQuestionAsync(question, ct);
        await _audit.LogAsync("survey.question_deleted", actor, tenantId: tenantId, resourceType: "survey", resourceId: question.SurveyId, ct: ct);
    }

    public async Task MoveQuestionAsync(Guid tenantId, Guid questionId, int delta, AuditContext actor, CancellationToken ct = default)
    {
        var question = await _surveys.GetQuestionAsync(tenantId, questionId, ct) ?? throw new KeyNotFoundException("Question not found.");
        var ordered = (await _surveys.ListQuestionsAsync(question.SurveyId, ct)).ToList();
        var idx = ordered.FindIndex(q => q.Id == questionId);
        var swap = idx + delta;
        if (idx < 0 || swap < 0 || swap >= ordered.Count) return;

        (ordered[idx].Order, ordered[swap].Order) = (ordered[swap].Order, ordered[idx].Order);
        await _surveys.UpdateQuestionAsync(ordered[idx], ct);
        await _surveys.UpdateQuestionAsync(ordered[swap], ct);
    }

    // --- Gate + runner ---

    public async Task<IReadOnlyList<PendingSurvey>> GetPendingAsync(Guid tenantId, Guid userId, Guid? sessionId, Guid? applicationId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var active = (await _surveys.ListPublishedAsync(tenantId, ct))
            .Where(s => s.StartsAt is null || now >= s.StartsAt)
            .Where(s => s.CloseDate is null || now < s.CloseDate)
            .ToList();
        if (active.Count == 0) return Array.Empty<PendingSurvey>();

        var targetings = active.ToDictionary(s => s.Id, s => PolicyTargetingJson.Parse(s.Targeting));
        string? authCategory = null;
        if (targetings.Values.Any(t => t.Audience == Audiences.AuthMethods))
            authCategory = await TargetingEvaluator.AuthCategoryAsync(_logins, tenantId, userId, ct);
        HashSet<string>? providers = null;
        if (targetings.Values.Any(t => t.Audience == Audiences.Providers))
            providers = await TargetingEvaluator.LinkedProvidersAsync(_social, tenantId, userId, ct);

        var pending = new List<PendingSurvey>();
        foreach (var survey in active)
        {
            if (!TargetingEvaluator.Matches(targetings[survey.Id], applicationId, authCategory, providers)) continue;

            var cutoff = Max(survey.PublishedAt, survey.ConsentResetAt);
            var responses = (await _surveys.ListUserResponsesForSurveyAsync(tenantId, survey.Id, userId, ct))
                .Where(r => cutoff is null || r.StartedAt > cutoff).ToList();

            var satisfied = survey.EnforcementMode == PolicyEnforcementMode.Optional
                ? responses.Any(r => r.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.Declined)
                : responses.Any(r => r.Status == SurveyResponseStatus.Completed);
            if (satisfied) continue;

            var skippedThisSession = sessionId is { } sid && responses.Any(r => r.Status == SurveyResponseStatus.Skipped && r.SessionId == sid);
            if (skippedThisSession && survey.EnforcementMode != PolicyEnforcementMode.Mandatory)
            {
                var pastDeadline = survey.EnforcementMode == PolicyEnforcementMode.SkippableUntil
                    && survey.SkipDeadline is { } dl && now >= dl;
                if (!pastDeadline) continue;
            }

            var (hardBlock, allowSkip, allowReject) = Flags(survey.EnforcementMode, survey.SkipDeadline, now);
            pending.Add(new PendingSurvey(survey.Id, survey.Title, survey.Description,
                survey.EnforcementMode, hardBlock, allowSkip, allowReject));
        }
        return pending;
    }

    public async Task<SurveyForRunner?> GetForRunnerAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
    {
        var survey = await _surveys.GetAsync(tenantId, surveyId, ct);
        if (survey is null) return null;

        var questions = (await ListQuestionsAsync(tenantId, surveyId, ct)).ToList();
        if (survey.RandomizeQuestions)
            questions = questions.OrderBy(_ => Guid.NewGuid()).ToList();

        var prepared = questions.Select(qo =>
        {
            var opts = qo.Options;
            if (qo.Question.RandomizeOptions) opts = opts.OrderBy(_ => Guid.NewGuid()).ToList();
            return new QuestionWithOptions(qo.Question, opts);
        }).ToList();

        return new SurveyForRunner(survey, prepared);
    }

    public async Task SubmitAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId,
        IReadOnlyList<SurveyAnswerInput> answers, AuditContext actor, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, surveyId, ct);
        var questions = await _surveys.ListQuestionsAsync(surveyId, ct);
        var byId = questions.ToDictionary(q => q.Id);
        var answerByQ = answers.ToDictionary(a => a.QuestionId);

        // Enforce required questions.
        foreach (var q in questions.Where(q => q.Required))
        {
            var hasAnswer = answerByQ.TryGetValue(q.Id, out var a) && a is not null &&
                (!string.IsNullOrWhiteSpace(a.Text) || a.Number is not null || a.OptionIds.Count > 0);
            if (!hasAnswer) throw new SurveyInvalidException($"\"{q.Title}\" is required.");
        }

        var response = new SurveyResponse
        {
            SurveyId = surveyId, TenantId = tenantId, UserId = userId, SessionId = sessionId,
            Status = SurveyResponseStatus.Completed, StartedAt = DateTimeOffset.UtcNow, SubmittedAt = DateTimeOffset.UtcNow
        };
        await _surveys.AddResponseAsync(response, ct);

        var rows = new List<SurveyAnswer>();
        foreach (var input in answers)
        {
            if (!byId.TryGetValue(input.QuestionId, out var q)) continue;
            rows.Add(new SurveyAnswer
            {
                ResponseId = response.Id, QuestionId = q.Id, TenantId = tenantId,
                TextValue = string.IsNullOrWhiteSpace(input.Text) ? null : input.Text.Trim(),
                NumberValue = input.Number,
                OptionIds = input.OptionIds.Count > 0 ? JsonSerializer.Serialize(input.OptionIds) : null,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        if (rows.Count > 0) await _surveys.AddAnswersAsync(rows, ct);

        await _audit.LogAsync("survey.submitted", actor, tenantId: tenantId, resourceType: "survey", resourceId: surveyId, ct: ct);
    }

    public Task SkipAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId, AuditContext actor, CancellationToken ct = default)
        => RecordStatusAsync(tenantId, surveyId, userId, sessionId, SurveyResponseStatus.Skipped, actor, ct);

    public Task DeclineAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId, AuditContext actor, CancellationToken ct = default)
        => RecordStatusAsync(tenantId, surveyId, userId, sessionId, SurveyResponseStatus.Declined, actor, ct);

    // --- Reporting + portal ---

    public async Task<SurveyReport> GetReportAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
    {
        var survey = await Require(tenantId, surveyId, ct);
        var responses = await _surveys.ListResponsesForSurveyAsync(tenantId, surveyId, ct);
        var questions = await ListQuestionsAsync(tenantId, surveyId, ct);
        var answers = await _surveys.ListAnswersForSurveyAsync(tenantId, surveyId, ct);
        var answersByQ = answers.GroupBy(a => a.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

        var qReports = new List<QuestionReport>();
        foreach (var qo in questions)
        {
            var q = qo.Question;
            answersByQ.TryGetValue(q.Id, out var qa);
            qa ??= new List<SurveyAnswer>();

            IReadOnlyList<(string, int)> optionCounts = Array.Empty<(string, int)>();
            double? avg = null;
            IReadOnlyList<string> samples = Array.Empty<string>();

            if (RequiresOptions(q.Type))
            {
                var labelById = qo.Options.ToDictionary(o => o.Id.ToString(), o => o.Label);
                var counts = new Dictionary<string, int>();
                foreach (var a in qa)
                {
                    foreach (var id in DeserializeOptionIds(a.OptionIds))
                    {
                        var label = labelById.TryGetValue(id, out var l) ? l : "(removed)";
                        counts[label] = counts.GetValueOrDefault(label) + 1;
                    }
                }
                optionCounts = counts.Select(kv => (kv.Key, kv.Value)).OrderByDescending(x => x.Value).ToList();
            }
            else if (q.Type is SurveyQuestionType.YesNo)
            {
                var counts = qa.Where(a => a.TextValue is not null)
                    .GroupBy(a => a.TextValue!).ToDictionary(g => g.Key, g => g.Count());
                optionCounts = counts.Select(kv => (kv.Key, kv.Value)).ToList();
            }
            else if (q.Type is SurveyQuestionType.Number or SurveyQuestionType.Rating)
            {
                var nums = qa.Where(a => a.NumberValue is not null).Select(a => a.NumberValue!.Value).ToList();
                if (nums.Count > 0) avg = nums.Average();
            }
            else
            {
                samples = qa.Where(a => !string.IsNullOrWhiteSpace(a.TextValue)).Select(a => a.TextValue!).Take(20).ToList();
            }

            qReports.Add(new QuestionReport
            {
                Question = q, AnswerCount = qa.Count, OptionCounts = optionCounts, Average = avg, TextSamples = samples
            });
        }

        return new SurveyReport
        {
            Survey = survey,
            Completed = responses.Count(r => r.Status == SurveyResponseStatus.Completed),
            InProgress = responses.Count(r => r.Status == SurveyResponseStatus.InProgress),
            Skipped = responses.Count(r => r.Status == SurveyResponseStatus.Skipped),
            Declined = responses.Count(r => r.Status == SurveyResponseStatus.Declined),
            Questions = qReports
        };
    }

    public Task<IReadOnlyList<SurveyResponse>> ListUserResponsesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _surveys.ListUserResponsesAsync(tenantId, userId, ct);

    // --- helpers ---

    private async Task RecordStatusAsync(Guid tenantId, Guid surveyId, Guid userId, Guid? sessionId,
        SurveyResponseStatus status, AuditContext actor, CancellationToken ct)
    {
        await _surveys.AddResponseAsync(new SurveyResponse
        {
            SurveyId = surveyId, TenantId = tenantId, UserId = userId, SessionId = sessionId,
            Status = status, StartedAt = DateTimeOffset.UtcNow,
            SubmittedAt = status == SurveyResponseStatus.Declined ? DateTimeOffset.UtcNow : null
        }, ct);
        await _audit.LogAsync($"survey.{status.ToString().ToLowerInvariant()}", actor, tenantId: tenantId,
            resourceType: "survey", resourceId: surveyId, ct: ct);
    }

    private static (bool HardBlock, bool AllowSkip, bool AllowReject) Flags(PolicyEnforcementMode mode, DateTimeOffset? skipDeadline, DateTimeOffset now) => mode switch
    {
        PolicyEnforcementMode.Mandatory => (true, false, false),
        PolicyEnforcementMode.Optional => (false, true, true),
        PolicyEnforcementMode.SkippableUntil when skipDeadline is { } dl && now >= dl => (true, false, false),
        PolicyEnforcementMode.SkippableUntil => (false, true, false),
        _ => (true, false, false)
    };

    private static bool RequiresOptions(SurveyQuestionType type)
        => type is SurveyQuestionType.SingleChoice or SurveyQuestionType.MultipleChoice or SurveyQuestionType.Dropdown;

    private static IEnumerable<string> DeserializeOptionIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch (JsonException) { return Array.Empty<string>(); }
    }

    private static DateTimeOffset? Max(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : (a > b ? a : b);

    private async Task<Survey> Require(Guid tenantId, Guid id, CancellationToken ct)
        => await _surveys.GetAsync(tenantId, id, ct) ?? throw new KeyNotFoundException($"Survey {id} not found.");

    private static string? Normalize(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
