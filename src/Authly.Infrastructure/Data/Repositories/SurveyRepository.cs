using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class SurveyRepository : ISurveyRepository
{
    private readonly AppDbContext _db;

    public SurveyRepository(AppDbContext db) => _db = db;

    // --- Surveys ---

    public async Task<IReadOnlyList<Survey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Surveys.Where(s => s.TenantId == tenantId).OrderByDescending(s => s.UpdatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Survey>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Surveys.Where(s => s.TenantId == tenantId && s.Status == PolicyStatus.Published).ToListAsync(ct);

    public Task<Survey?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Surveys.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id, ct);

    public async Task AddAsync(Survey survey, CancellationToken ct = default) { _db.Surveys.Add(survey); await _db.SaveChangesAsync(ct); }
    public async Task UpdateAsync(Survey survey, CancellationToken ct = default) { _db.Surveys.Update(survey); await _db.SaveChangesAsync(ct); }
    public async Task DeleteAsync(Survey survey, CancellationToken ct = default) { _db.Surveys.Remove(survey); await _db.SaveChangesAsync(ct); }

    // --- Questions & options ---

    public async Task<IReadOnlyList<SurveyQuestion>> ListQuestionsAsync(Guid surveyId, CancellationToken ct = default)
        => await _db.SurveyQuestions.Where(q => q.SurveyId == surveyId).OrderBy(q => q.Order).ToListAsync(ct);

    public Task<SurveyQuestion?> GetQuestionAsync(Guid tenantId, Guid questionId, CancellationToken ct = default)
        => _db.SurveyQuestions.FirstOrDefaultAsync(q => q.TenantId == tenantId && q.Id == questionId, ct);

    public async Task AddQuestionAsync(SurveyQuestion question, CancellationToken ct = default) { _db.SurveyQuestions.Add(question); await _db.SaveChangesAsync(ct); }
    public async Task UpdateQuestionAsync(SurveyQuestion question, CancellationToken ct = default) { _db.SurveyQuestions.Update(question); await _db.SaveChangesAsync(ct); }
    public async Task DeleteQuestionAsync(SurveyQuestion question, CancellationToken ct = default) { _db.SurveyQuestions.Remove(question); await _db.SaveChangesAsync(ct); }

    public async Task<IReadOnlyList<SurveyQuestionOption>> ListOptionsBySurveyAsync(Guid surveyId, CancellationToken ct = default)
        => await _db.SurveyQuestionOptions.Where(o => o.SurveyId == surveyId).OrderBy(o => o.Order).ToListAsync(ct);

    public async Task AddOptionAsync(SurveyQuestionOption option, CancellationToken ct = default) { _db.SurveyQuestionOptions.Add(option); await _db.SaveChangesAsync(ct); }

    public async Task DeleteOptionsForQuestionAsync(Guid questionId, CancellationToken ct = default)
        => await _db.SurveyQuestionOptions.Where(o => o.QuestionId == questionId).ExecuteDeleteAsync(ct);

    // --- Responses & answers ---

    public async Task<IReadOnlyList<SurveyResponse>> ListUserResponsesForSurveyAsync(Guid tenantId, Guid surveyId, Guid userId, CancellationToken ct = default)
        => await _db.SurveyResponses
            .Where(r => r.TenantId == tenantId && r.SurveyId == surveyId && r.UserId == userId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SurveyResponse>> ListUserResponsesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.SurveyResponses
            .Where(r => r.TenantId == tenantId && r.UserId == userId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SurveyResponse>> ListResponsesForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
        => await _db.SurveyResponses
            .Where(r => r.TenantId == tenantId && r.SurveyId == surveyId)
            .OrderByDescending(r => r.StartedAt)
            .ToListAsync(ct);

    public async Task AddResponseAsync(SurveyResponse response, CancellationToken ct = default) { _db.SurveyResponses.Add(response); await _db.SaveChangesAsync(ct); }

    public async Task AddAnswersAsync(IEnumerable<SurveyAnswer> answers, CancellationToken ct = default)
    {
        _db.SurveyAnswers.AddRange(answers);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SurveyAnswer>> ListAnswersForSurveyAsync(Guid tenantId, Guid surveyId, CancellationToken ct = default)
        => await (from a in _db.SurveyAnswers
                  join r in _db.SurveyResponses on a.ResponseId equals r.Id
                  where a.TenantId == tenantId && r.SurveyId == surveyId && r.Status == SurveyResponseStatus.Completed
                  select a).ToListAsync(ct);
}
