using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public class Question : AuditableEntity, IAggregateRoot
{
    public Guid SubjectId { get; private set; }
    public Guid? TopicId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string? ExpectedAnswer { get; private set; }
    public BloomLevel BloomLevel { get; private set; }
    public QuestionDifficulty Difficulty { get; private set; }
    public QuestionSourceType SourceType { get; private set; }
    public QuestionStatus Status { get; private set; }

    private Question() { }

    public Question(Guid subjectId, string content, BloomLevel bloomLevel,
        QuestionDifficulty difficulty, string? expectedAnswer = null, Topic? topic = null,
        QuestionSourceType sourceType = QuestionSourceType.Manual,
        QuestionStatus status = QuestionStatus.Draft)
    {
        DomainRules.RequiredId(subjectId, nameof(SubjectId));
        DomainRules.DefinedEnum(sourceType, nameof(SourceType));
        DomainRules.DefinedEnum(status, nameof(Status));
        SubjectId = subjectId;
        SourceType = sourceType;
        Apply(content, expectedAnswer, bloomLevel, difficulty, topic);
        Status = status;
    }

    // SubjectId is fixed; the topic may change within the same subject.
    // Any edit sends the question back to Draft so it has to be reviewed again.
    public void Update(string content, string? expectedAnswer, BloomLevel bloomLevel,
        QuestionDifficulty difficulty, Topic? topic)
    {
        Apply(content, expectedAnswer, bloomLevel, difficulty, topic);
        Status = QuestionStatus.Draft;
    }

    public void Approve() => Review(QuestionStatus.Approved);

    public void Reject() => Review(QuestionStatus.Rejected);

    private void Review(QuestionStatus result)
    {
        if (Status != QuestionStatus.Draft)
            throw new DomainValidationException("Only draft questions can be reviewed.");
        Status = result;
    }

    // Validates everything before assigning so a failed update leaves the question unchanged.
    private void Apply(string content, string? expectedAnswer, BloomLevel bloomLevel,
        QuestionDifficulty difficulty, Topic? topic)
    {
        var normalizedContent = DomainRules.RequiredText(content, nameof(Content));
        DomainRules.DefinedEnum(bloomLevel, nameof(BloomLevel));
        DomainRules.DefinedEnum(difficulty, nameof(Difficulty));
        if (topic is not null && topic.SubjectId != SubjectId)
            throw new DomainValidationException("The topic must belong to the question's subject.");
        Content = normalizedContent;
        ExpectedAnswer = string.IsNullOrWhiteSpace(expectedAnswer) ? null : expectedAnswer.Trim();
        BloomLevel = bloomLevel;
        Difficulty = difficulty;
        TopicId = topic?.Id;
    }
}
