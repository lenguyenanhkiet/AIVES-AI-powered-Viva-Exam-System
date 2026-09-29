using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class QuestionDomainTests
{
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static Question NewQuestion() =>
        new(SubjectId, "Explain DI.", BloomLevel.Understand, QuestionDifficulty.Medium);

    [Fact]
    public void Update_replaces_fields_and_returns_reviewed_question_to_draft()
    {
        var topic = new Topic(SubjectId, "DI");
        var question = NewQuestion();
        question.Approve();

        question.Update(" Compare lifetimes. ", "  ", BloomLevel.Analyze, QuestionDifficulty.Hard, topic);

        Assert.Equal("Compare lifetimes.", question.Content);
        Assert.Null(question.ExpectedAnswer);
        Assert.Equal(BloomLevel.Analyze, question.BloomLevel);
        Assert.Equal(QuestionDifficulty.Hard, question.Difficulty);
        Assert.Equal(topic.Id, question.TopicId);
        Assert.Equal(QuestionStatus.Draft, question.Status);
    }

    [Fact]
    public void Invalid_update_does_not_mutate()
    {
        var question = NewQuestion();
        var otherSubjectTopic = new Topic(Guid.NewGuid(), "Other");

        Assert.Throws<DomainValidationException>(() =>
            question.Update("New", null, BloomLevel.Apply, QuestionDifficulty.Easy, otherSubjectTopic));
        Assert.Throws<DomainValidationException>(() =>
            question.Update(" ", null, BloomLevel.Apply, QuestionDifficulty.Easy, null));
        Assert.Throws<DomainValidationException>(() =>
            question.Update("New", null, (BloomLevel)99, QuestionDifficulty.Easy, null));

        Assert.Equal("Explain DI.", question.Content);
        Assert.Equal(BloomLevel.Understand, question.BloomLevel);
        Assert.Null(question.TopicId);
    }

    [Fact]
    public void Only_draft_questions_can_be_reviewed()
    {
        var approved = NewQuestion();
        approved.Approve();
        Assert.Equal(QuestionStatus.Approved, approved.Status);
        Assert.Throws<DomainValidationException>(approved.Approve);
        Assert.Throws<DomainValidationException>(approved.Reject);

        var rejected = NewQuestion();
        rejected.Reject();
        Assert.Equal(QuestionStatus.Rejected, rejected.Status);
        Assert.Throws<DomainValidationException>(rejected.Approve);
    }
}
