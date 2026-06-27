using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for the surveys engine. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface ISurveyRepository
{
    // --- Surveys ---
    Task<IReadOnlyList<Survey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<Survey>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default);
    Task<Survey?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Survey survey, CancellationToken ct = default);
    Task UpdateAsync(Survey survey, CancellationToken ct = default);
    Task DeleteAsync(Survey survey, CancellationToken ct = default);

    // --- Questions & options ---
    Task<IReadOnlyList<SurveyQuestion>> ListQuestionsAsync(Guid surveyId, CancellationToken ct = default);
    Task<SurveyQuestion?> GetQuestionAsync(Guid tenantId, Guid questionId, CancellationToken ct = default);
    Task AddQuestionAsync(SurveyQuestion question, CancellationToken ct = default);
    Task UpdateQuestionAsync(SurveyQuestion question, CancellationToken ct = default);
    Task DeleteQuestionAsync(SurveyQuestion question, CancellationToken ct = default);

    Task<IReadOnlyList<SurveyQuestionOption>> ListOptionsBySurveyAsync(Guid surveyId, CancellationToken ct = default);
    Task AddOptionAsync(SurveyQuestionOption option, CancellationToken ct = default);
    Task DeleteOptionsForQuestionAsync(Guid questionId, CancellationToken ct = default);

    // --- Responses & answers ---
    Task<IReadOnlyList<SurveyResponse>> ListUserResponsesForSurveyAsync(Guid tenantId, Guid surveyId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SurveyResponse>> ListUserResponsesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<SurveyResponse>> ListResponsesForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default);
    Task AddResponseAsync(SurveyResponse response, CancellationToken ct = default);
    Task AddAnswersAsync(IEnumerable<SurveyAnswer> answers, CancellationToken ct = default);
    Task<IReadOnlyList<SurveyAnswer>> ListAnswersForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default);
}
