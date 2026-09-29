using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class RubricDomainTests
{
    private static CriterionDefinition Criterion(decimal score = 2, int order = 1, Guid? id = null) =>
        new(id, "Definition", null, null, score, order);

    [Fact]
    public void Update_preserves_existing_ids_and_recalculates_total()
    {
        var rubric = new Rubric(Guid.NewGuid(), "Rubric", null, [Criterion(), Criterion(3, 2)]);
        var original = rubric.Criteria.First();
        rubric.Update("Updated", null, [Criterion(4, 2, original.Id), Criterion(1, 1)]);
        Assert.Equal(5, rubric.TotalScore);
        Assert.Contains(rubric.Criteria, x => ReferenceEquals(x, original) && x.MaxScore == 4);
        Assert.Equal(2, rubric.Criteria.Count);
    }

    [Fact]
    public void Failed_update_does_not_partially_mutate_the_aggregate()
    {
        var rubric = new Rubric(Guid.NewGuid(), "Original", null, [Criterion()]);
        var id = rubric.Criteria.Single().Id;
        Assert.Throws<DomainValidationException>(() => rubric.Update("Changed", null,
            [Criterion(3, 1, id), Criterion(4, 1)]));
        Assert.Equal("Original", rubric.Name);
        Assert.Equal(2, rubric.TotalScore);
        Assert.Equal(id, rubric.Criteria.Single().Id);
        Assert.Equal(2, rubric.Criteria.Single().MaxScore);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("1000")]
    public void Rejects_invalid_scores(string value)
    {
        var score = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<DomainValidationException>(() =>
            new Rubric(Guid.NewGuid(), "Rubric", null, [Criterion(score)]));
    }

    [Fact]
    public void Rejects_overflowing_total_empty_criteria_and_foreign_ids()
    {
        Assert.Throws<DomainValidationException>(() => new Rubric(Guid.NewGuid(), "Rubric", null,
            [Criterion(600), Criterion(500, 2)]));
        Assert.Throws<DomainValidationException>(() => new Rubric(Guid.NewGuid(), "Rubric", null, []));
        var rubric = new Rubric(Guid.NewGuid(), "Rubric", null, [Criterion()]);
        Assert.Throws<DomainValidationException>(() => rubric.Update("Rubric", null,
            [Criterion(id: Guid.NewGuid())]));
    }

    [Fact]
    public void Question_rejects_topic_from_another_subject()
    {
        var topic = new Topic(Guid.NewGuid(), "Topic");
        Assert.Throws<DomainValidationException>(() => new Question(Guid.NewGuid(), "Question?",
            BloomLevel.Remember, QuestionDifficulty.Easy, topic: topic));
    }
}
