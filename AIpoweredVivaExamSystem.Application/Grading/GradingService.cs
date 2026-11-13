using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;

namespace AIpoweredVivaExamSystem.Application.Grading;

public sealed class GradingService(IGradingStore store, ExamAccessGuard guard, ICurrentUser actor, IAiRubricGrader grader)
{
    public async Task<GradingExamList> ListAsync(Guid examId, CancellationToken ct)
    { await guard.CanManageExamAsync(examId, ct); return await store.ListAsync(examId, ct); }
    private async Task AuthorizeAsync(Guid sessionId, CancellationToken ct)
    { guard.RequireManager(); await guard.CanManageExamAsync(await store.ExamForSessionAsync(sessionId, ct), ct); }
    public async Task<SessionReview> ReviewAsync(Guid sessionId, CancellationToken ct)
    { await AuthorizeAsync(sessionId, ct); return await store.ReviewAsync(sessionId, ct); }
    public async Task SaveAsync(Guid sessionId, GradeInput input, bool finalise, CancellationToken ct)
    { await AuthorizeAsync(sessionId, ct); await store.SaveAsync(sessionId, input, finalise, actor.Id, ct); }
    public async Task ReopenAsync(Guid sessionId, string version, CancellationToken ct)
    { await AuthorizeAsync(sessionId, ct); await store.ReopenAsync(sessionId, version, actor.Id, ct); }
    public async Task<QuestionReview> SuggestAsync(Guid sessionId, Guid questionId, bool regenerate, CancellationToken ct)
    {
        await AuthorizeAsync(sessionId, ct);
        var work = await store.BeginSuggestionAsync(sessionId, questionId, regenerate, ct);
        if (work is not null)
        {
            try
            {
                var context=work.Value.Context;
                var raw=context.Conversation.All(t=>string.IsNullOrWhiteSpace(t.Transcript))
                    ? GradingRules.NoAnswerSuggestion(context.Criteria)
                    : await grader.SuggestAsync(context,ct);
                var suggestion = GradingRules.ValidateSuggestion(context,raw);
                await store.FinishSuggestionAsync(questionId, work.Value.AttemptId, suggestion, null, ct);
            }
            catch (Exception ex) when (ex is ResourceConflictException or HttpRequestException or OperationCanceledException)
            {
                await store.FinishSuggestionAsync(questionId, work.Value.AttemptId, null,
                    GradingRules.UnavailableSuggestionMessage, CancellationToken.None);
                if (ct.IsCancellationRequested) throw;
            }
        }
        return (await store.ReviewAsync(sessionId, ct)).Questions.Single(q => q.SessionQuestionId == questionId);
    }
    public async Task<GradingAudio> AudioAsync(Guid audioId, CancellationToken ct)
    {
        guard.RequireManager();
        var audio = await store.AudioAsync(audioId, ct);
        await guard.CanManageExamAsync(audio.ExamId, ct);
        return audio;
    }
}
