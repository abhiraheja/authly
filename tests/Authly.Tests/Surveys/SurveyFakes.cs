using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;

namespace Authly.Tests.Surveys;

internal sealed class FakeSurveyRepo : ISurveyRepository
{
    public readonly List<Survey> Surveys = new();
    public readonly List<SurveyQuestion> Questions = new();
    public readonly List<SurveyQuestionOption> Options = new();
    public readonly List<SurveyResponse> Responses = new();
    public readonly List<SurveyAnswer> Answers = new();

    public Task<IReadOnlyList<Survey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Survey>>(Surveys.Where(s => s.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<Survey>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Survey>>(Surveys.Where(s => s.TenantId == tenantId && s.Status == PolicyStatus.Published).ToList());
    public Task<Survey?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Surveys.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));
    public Task AddAsync(Survey survey, CancellationToken ct = default) { if (survey.Id == Guid.Empty) survey.Id = Guid.NewGuid(); Surveys.Add(survey); return Task.CompletedTask; }
    public Task UpdateAsync(Survey survey, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Survey survey, CancellationToken ct = default) { Surveys.Remove(survey); return Task.CompletedTask; }

    public Task<IReadOnlyList<SurveyQuestion>> ListQuestionsAsync(Guid surveyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SurveyQuestion>>(Questions.Where(q => q.SurveyId == surveyId).OrderBy(q => q.Order).ToList());
    public Task<SurveyQuestion?> GetQuestionAsync(Guid tenantId, Guid questionId, CancellationToken ct = default)
        => Task.FromResult(Questions.FirstOrDefault(q => q.TenantId == tenantId && q.Id == questionId));
    public Task AddQuestionAsync(SurveyQuestion question, CancellationToken ct = default) { if (question.Id == Guid.Empty) question.Id = Guid.NewGuid(); Questions.Add(question); return Task.CompletedTask; }
    public Task UpdateQuestionAsync(SurveyQuestion question, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteQuestionAsync(SurveyQuestion question, CancellationToken ct = default) { Questions.Remove(question); return Task.CompletedTask; }

    public Task<IReadOnlyList<SurveyQuestionOption>> ListOptionsBySurveyAsync(Guid surveyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SurveyQuestionOption>>(Options.Where(o => o.SurveyId == surveyId).OrderBy(o => o.Order).ToList());
    public Task AddOptionAsync(SurveyQuestionOption option, CancellationToken ct = default) { if (option.Id == Guid.Empty) option.Id = Guid.NewGuid(); Options.Add(option); return Task.CompletedTask; }
    public Task DeleteOptionsForQuestionAsync(Guid questionId, CancellationToken ct = default) { Options.RemoveAll(o => o.QuestionId == questionId); return Task.CompletedTask; }

    public Task<IReadOnlyList<SurveyResponse>> ListUserResponsesForSurveyAsync(Guid tenantId, Guid surveyId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SurveyResponse>>(Responses.Where(r => r.TenantId == tenantId && r.SurveyId == surveyId && r.UserId == userId).ToList());
    public Task<IReadOnlyList<SurveyResponse>> ListUserResponsesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SurveyResponse>>(Responses.Where(r => r.TenantId == tenantId && r.UserId == userId).ToList());
    public Task<IReadOnlyList<SurveyResponse>> ListResponsesForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SurveyResponse>>(Responses.Where(r => r.TenantId == tenantId && r.SurveyId == surveyId).ToList());
    public Task AddResponseAsync(SurveyResponse response, CancellationToken ct = default) { if (response.Id == Guid.Empty) response.Id = Guid.NewGuid(); Responses.Add(response); return Task.CompletedTask; }
    public Task AddAnswersAsync(IEnumerable<SurveyAnswer> answers, CancellationToken ct = default) { Answers.AddRange(answers); return Task.CompletedTask; }
    public Task<IReadOnlyList<SurveyAnswer>> ListAnswersForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
    {
        var respIds = Responses.Where(r => r.SurveyId == surveyId && r.Status == SurveyResponseStatus.Completed).Select(r => r.Id).ToHashSet();
        return Task.FromResult<IReadOnlyList<SurveyAnswer>>(Answers.Where(a => a.TenantId == tenantId && respIds.Contains(a.ResponseId)).ToList());
    }
}
