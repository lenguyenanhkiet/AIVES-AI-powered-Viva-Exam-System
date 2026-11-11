using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class StudentAnswer : AuditableEntity
{
    public Guid SessionQuestionId { get; private set; }
    public Guid? ParentAnswerId { get; private set; }
    public AnswerType AnswerType { get; private set; }
    public int FollowUpLevel { get; private set; }
    public string PromptText { get; private set; } = "";
    public string? Transcript { get; private set; }
    public DateTimeOffset AskedAt { get; private set; }
    public DateTimeOffset? AnsweredAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private StudentAnswer() { }

    public StudentAnswer(Guid sessionQuestionId, AnswerType answerType, int followUpLevel,
        string promptText, DateTimeOffset askedAt, Guid? parentAnswerId = null)
    {
        DomainRules.RequiredId(sessionQuestionId, nameof(SessionQuestionId));
        DomainRules.DefinedEnum(answerType, nameof(AnswerType));
        var validPrompt = DomainRules.RequiredText(promptText, nameof(PromptText), 4000);

        if (answerType == AnswerType.Main)
        {
            if (followUpLevel != 0 || parentAnswerId.HasValue)
                throw new DomainValidationException("Main question turn must have FollowUpLevel = 0 and no parent answer.");
        }
        else
        {
            if (followUpLevel < 1 || parentAnswerId is null || parentAnswerId == Guid.Empty)
                throw new DomainValidationException("Follow-up turn requires FollowUpLevel >= 1 and a valid ParentAnswerId.");
        }

        SessionQuestionId = sessionQuestionId;
        ParentAnswerId = parentAnswerId;
        AnswerType = answerType;
        FollowUpLevel = followUpLevel;
        PromptText = validPrompt;
        AskedAt = askedAt;
    }

    public void SubmitResponse(string transcript, DateTimeOffset answeredAt)
    {
        if (AnsweredAt.HasValue)
            throw new DomainValidationException("This question turn has already been answered.");
        var validTranscript = DomainRules.RequiredText(transcript, nameof(Transcript), 8000);
        if (answeredAt < AskedAt)
            throw new DomainValidationException("Answer timestamp cannot precede the question prompt.");

        Transcript = validTranscript;
        AnsweredAt = answeredAt;
    }
}

public sealed class AnswerAnalysis : BaseEntity
{
    public Guid StudentAnswerId { get; private set; }
    public bool IsComplete { get; private set; }
    public string? MissingConcepts { get; private set; }
    public string? AnalysisSummary { get; private set; }
    public string? FollowUpPrompt { get; private set; }
    public string ModelName { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    private AnswerAnalysis() { }

    public AnswerAnalysis(Guid studentAnswerId, bool isComplete, string? missingConcepts,
        string? analysisSummary, string? followUpPrompt, string modelName, DateTimeOffset? createdAt = null)
    {
        DomainRules.RequiredId(studentAnswerId, nameof(StudentAnswerId));
        var validModel = DomainRules.RequiredText(modelName, nameof(ModelName), 100);
        var normalizedMissing = NormalizeOptional(missingConcepts, nameof(MissingConcepts), 2000);
        var normalizedSummary = NormalizeOptional(analysisSummary, nameof(AnalysisSummary), 4000);
        var normalizedFollowUp = NormalizeOptional(followUpPrompt, nameof(FollowUpPrompt), 4000);

        StudentAnswerId = studentAnswerId;
        IsComplete = isComplete;
        MissingConcepts = normalizedMissing;
        AnalysisSummary = normalizedSummary;
        FollowUpPrompt = isComplete ? null : normalizedFollowUp;
        ModelName = validModel;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }

    private static string? NormalizeOptional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainValidationException($"{field} must not exceed {maxLength} characters.");
        return trimmed;
    }
}

public sealed class AnswerAudio : BaseEntity
{
    public Guid StudentAnswerId { get; private set; }
    public string FilePath { get; private set; } = "";
    public string MimeType { get; private set; } = "";
    public int? DurationSeconds { get; private set; }
    public long FileSizeBytes { get; private set; }
    public string ContentHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }

    private AnswerAudio() { }

    public AnswerAudio(Guid studentAnswerId, string filePath, string mimeType,
        long fileSizeBytes, string contentHash, int? durationSeconds = null, DateTimeOffset? createdAt = null)
    {
        DomainRules.RequiredId(studentAnswerId, nameof(StudentAnswerId));
        var validPath = DomainRules.RequiredText(filePath, nameof(FilePath), 1000);
        var validMime = DomainRules.RequiredText(mimeType, nameof(MimeType), 100);
        var validHash = DomainRules.RequiredText(contentHash, nameof(ContentHash), 64);
        if (fileSizeBytes <= 0 || fileSizeBytes > 20 * 1024 * 1024)
            throw new DomainValidationException("Audio file size must be between 1 byte and 20 MB.");
        if (durationSeconds is < 0)
            throw new DomainValidationException("DurationSeconds cannot be negative.");

        StudentAnswerId = studentAnswerId;
        FilePath = validPath;
        MimeType = validMime;
        FileSizeBytes = fileSizeBytes;
        ContentHash = validHash;
        DurationSeconds = durationSeconds;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
    }
}

