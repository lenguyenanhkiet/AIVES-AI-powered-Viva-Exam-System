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
        DomainRules.DefinedEnum(bloomLevel, nameof(BloomLevel));
        DomainRules.DefinedEnum(difficulty, nameof(Difficulty));
        DomainRules.DefinedEnum(sourceType, nameof(SourceType));
        DomainRules.DefinedEnum(status, nameof(Status));
        if (topic is not null && topic.SubjectId != subjectId)
            throw new DomainValidationException("The topic must belong to the question's subject.");
        SubjectId = subjectId;
        TopicId = topic?.Id;
        Content = DomainRules.RequiredText(content, nameof(Content));
        ExpectedAnswer = expectedAnswer;
        BloomLevel = bloomLevel;
        Difficulty = difficulty;
        SourceType = sourceType;
        Status = status;
    }
}
