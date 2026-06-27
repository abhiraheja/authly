using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Policies;
using Authly.Modules.Common;
using Authly.Modules.Surveys;
using Authly.Tests.Policies; // reuse FakeLoginHistoryRepo / FakeSocialIdentityRepo / NoopAudit
using Xunit;

namespace Authly.Tests.Surveys;

public sealed class SurveyServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private static readonly AuditContext Actor = new(User, "user");

    private readonly FakeSurveyRepo _repo = new();
    private readonly SurveyService _sut;

    public SurveyServiceTests()
        => _sut = new SurveyService(_repo, new FakeLoginHistoryRepo(), new FakeSocialIdentityRepo(), new FakeUserRoleRepo(), new NoopAudit());

    private Survey Publish(PolicyEnforcementMode mode, DateTimeOffset? skipDeadline = null, DateTimeOffset? closeDate = null)
    {
        var survey = new Survey
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Title = "Feedback",
            Status = PolicyStatus.Published, EnforcementMode = mode,
            SkipDeadline = skipDeadline, CloseDate = closeDate,
            Targeting = PolicyTargetingJson.Serialize(PolicyTargeting.All()),
            PublishedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        _repo.Surveys.Add(survey);
        _repo.Questions.Add(new SurveyQuestion { Id = Guid.NewGuid(), SurveyId = survey.Id, TenantId = Tenant, Order = 0, Type = SurveyQuestionType.ShortText, Title = "Q1" });
        return survey;
    }

    [Fact]
    public async Task Optional_survey_is_pending_and_never_blocks()
    {
        Publish(PolicyEnforcementMode.Optional);
        var pending = await _sut.GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        var p = Assert.Single(pending);
        Assert.False(p.HardBlock);
        Assert.True(p.AllowSkip);
        Assert.True(p.AllowReject);
    }

    [Fact]
    public async Task Completed_response_clears_pending()
    {
        var s = Publish(PolicyEnforcementMode.Mandatory);
        _repo.Responses.Add(new SurveyResponse { Id = Guid.NewGuid(), SurveyId = s.Id, TenantId = Tenant, UserId = User, Status = SurveyResponseStatus.Completed, StartedAt = DateTimeOffset.UtcNow });
        Assert.Empty(await _sut.GetPendingAsync(Tenant, User, Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Mandatory_unanswered_hard_blocks()
    {
        Publish(PolicyEnforcementMode.Mandatory);
        var p = Assert.Single(await _sut.GetPendingAsync(Tenant, User, Guid.NewGuid(), null));
        Assert.True(p.HardBlock);
        Assert.False(p.AllowSkip);
    }

    [Fact]
    public async Task Skippable_skip_is_per_session()
    {
        var session = Guid.NewGuid();
        var s = Publish(PolicyEnforcementMode.SkippableUntil, skipDeadline: DateTimeOffset.UtcNow.AddDays(3));
        await _sut.SkipAsync(Tenant, s.Id, User, session, Actor);

        Assert.Empty(await _sut.GetPendingAsync(Tenant, User, session, null));       // same session cleared
        Assert.Single(await _sut.GetPendingAsync(Tenant, User, Guid.NewGuid(), null)); // new session asks again
    }

    [Fact]
    public async Task Closed_survey_is_not_pending()
    {
        Publish(PolicyEnforcementMode.Mandatory, closeDate: DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Empty(await _sut.GetPendingAsync(Tenant, User, Guid.NewGuid(), null));
    }

    [Fact]
    public async Task Submit_records_completed_response_with_answers()
    {
        var s = Publish(PolicyEnforcementMode.Optional);
        var q = _repo.Questions.First(x => x.SurveyId == s.Id);

        await _sut.SubmitAsync(Tenant, s.Id, User, Guid.NewGuid(),
            new[] { new SurveyAnswerInput { QuestionId = q.Id, Text = "Great product" } }, Actor);

        var resp = Assert.Single(_repo.Responses);
        Assert.Equal(SurveyResponseStatus.Completed, resp.Status);
        var ans = Assert.Single(_repo.Answers);
        Assert.Equal("Great product", ans.TextValue);
    }

    [Fact]
    public async Task Submit_throws_when_required_question_unanswered()
    {
        var s = Publish(PolicyEnforcementMode.Optional);
        var q = _repo.Questions.First(x => x.SurveyId == s.Id);
        q.Required = true;

        await Assert.ThrowsAsync<SurveyInvalidException>(() =>
            _sut.SubmitAsync(Tenant, s.Id, User, Guid.NewGuid(), Array.Empty<SurveyAnswerInput>(), Actor));
    }

    [Fact]
    public async Task Publish_requires_at_least_one_question()
    {
        var survey = new Survey { Id = Guid.NewGuid(), TenantId = Tenant, Title = "Empty", Status = PolicyStatus.Draft };
        _repo.Surveys.Add(survey);
        await Assert.ThrowsAsync<SurveyInvalidException>(() => _sut.PublishAsync(Tenant, survey.Id, Actor));
    }

    [Fact]
    public async Task ExportCsv_has_header_and_one_row_per_completed_response()
    {
        var s = Publish(PolicyEnforcementMode.Optional);
        var q = _repo.Questions.First(x => x.SurveyId == s.Id);
        await _sut.SubmitAsync(Tenant, s.Id, User, Guid.NewGuid(),
            new[] { new SurveyAnswerInput { QuestionId = q.Id, Text = "Great, thanks" } }, Actor);

        var csv = await _sut.ExportResponsesCsvAsync(Tenant, s.Id);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("response_id,submitted_at,", lines[0]);
        Assert.Contains("Q1", lines[0]);
        Assert.Equal(2, lines.Length);          // header + 1 response
        Assert.Contains("Great, thanks", lines[1]);
    }

    [Fact]
    public async Task Report_aggregates_choice_option_counts()
    {
        var survey = new Survey { Id = Guid.NewGuid(), TenantId = Tenant, Title = "Poll", Status = PolicyStatus.Published, PublishedAt = DateTimeOffset.UtcNow };
        _repo.Surveys.Add(survey);
        var q = new SurveyQuestion { Id = Guid.NewGuid(), SurveyId = survey.Id, TenantId = Tenant, Order = 0, Type = SurveyQuestionType.SingleChoice, Title = "Pick one" };
        _repo.Questions.Add(q);
        var optA = new SurveyQuestionOption { Id = Guid.NewGuid(), QuestionId = q.Id, SurveyId = survey.Id, TenantId = Tenant, Order = 0, Label = "A" };
        var optB = new SurveyQuestionOption { Id = Guid.NewGuid(), QuestionId = q.Id, SurveyId = survey.Id, TenantId = Tenant, Order = 1, Label = "B" };
        _repo.Options.Add(optA); _repo.Options.Add(optB);

        // Two users pick A, one picks B.
        foreach (var choice in new[] { optA.Id, optA.Id, optB.Id })
        {
            var resp = new SurveyResponse { Id = Guid.NewGuid(), SurveyId = survey.Id, TenantId = Tenant, UserId = Guid.NewGuid(), Status = SurveyResponseStatus.Completed, StartedAt = DateTimeOffset.UtcNow };
            _repo.Responses.Add(resp);
            _repo.Answers.Add(new SurveyAnswer { Id = Guid.NewGuid(), ResponseId = resp.Id, QuestionId = q.Id, TenantId = Tenant, OptionIds = System.Text.Json.JsonSerializer.Serialize(new[] { choice.ToString() }) });
        }

        var report = await _sut.GetReportAsync(Tenant, survey.Id);
        Assert.Equal(3, report.Completed);
        var qr = Assert.Single(report.Questions);
        Assert.Equal(("A", 2), qr.OptionCounts[0]);
        Assert.Equal(("B", 1), qr.OptionCounts[1]);
    }
}
