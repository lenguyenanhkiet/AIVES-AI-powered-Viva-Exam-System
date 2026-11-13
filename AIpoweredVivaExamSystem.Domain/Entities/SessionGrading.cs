using AIpoweredVivaExamSystem.Domain.Common;
namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class SessionGrading : BaseEntity
{
    public Guid VivaSessionId { get; private set; }
    public string? OverallComment { get; private set; }
    public bool IsFinalised { get; private set; }
    public Guid? EditedBy { get; private set; }
    public DateTimeOffset? EditedAt { get; private set; }
    public Guid? FinalisedBy { get; private set; }
    public DateTimeOffset? FinalisedAt { get; private set; }
    public Guid? ReopenedBy { get; private set; }
    public DateTimeOffset? ReopenedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    private SessionGrading() { }
    public SessionGrading(Guid sessionId) { DomainRules.RequiredId(sessionId,nameof(sessionId)); VivaSessionId=sessionId; }
    public void Save(string? comment,Guid lecturerId,bool finalise)
    {
        if(IsFinalised) throw new DomainValidationException("Mở lại bài đã chốt trước khi sửa điểm.");
        DomainRules.RequiredId(lecturerId,nameof(lecturerId));
        if(comment?.Length>4000) throw new DomainValidationException("Nhận xét chung quá dài.");
        OverallComment=comment?.Trim(); EditedBy=lecturerId; EditedAt=DateTimeOffset.UtcNow;
        if(finalise) { IsFinalised=true; FinalisedBy=lecturerId; FinalisedAt=EditedAt; }
    }
    public void Reopen(Guid lecturerId)
    {
        if(!IsFinalised) throw new DomainValidationException("Bài chấm chưa được chốt.");
        DomainRules.RequiredId(lecturerId,nameof(lecturerId));
        IsFinalised=false; ReopenedBy=lecturerId; ReopenedAt=DateTimeOffset.UtcNow; EditedBy=lecturerId; EditedAt=ReopenedAt;
    }
}

public sealed class QuestionGrading : BaseEntity
{
    public Guid SessionQuestionId { get; private set; }
    public string? AiSuggestionJson { get; private set; }
    public string? AiModel { get; private set; }
    public DateTimeOffset? SuggestedAt { get; private set; }
    public Guid? SuggestionAttemptId { get; private set; }
    public DateTimeOffset? SuggestionStartedAt { get; private set; }
    public string? SuggestionError { get; private set; }
    public string? LecturerScoresJson { get; private set; }
    public string? LecturerComment { get; private set; }
    private QuestionGrading() { }
    public QuestionGrading(Guid questionId) { DomainRules.RequiredId(questionId,nameof(questionId)); SessionQuestionId=questionId; }
    public void SetLecturerScores(string scoresJson,string? comment)
    { LecturerScoresJson=scoresJson; LecturerComment=comment?.Trim(); }
}
