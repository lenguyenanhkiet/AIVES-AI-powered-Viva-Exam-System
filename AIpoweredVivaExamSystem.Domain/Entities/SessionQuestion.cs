using System.Text.Json;
using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class SessionQuestion : BaseEntity
{
    public Guid VivaSessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int Position { get; private set; }
    public string ContentSnapshot { get; private set; } = "";
    // Internal grading data; never put the entity itself in a student response/ViewModel.
    public string RubricSnapshot { get; private set; } = "";
    private SessionQuestion() { }
    public SessionQuestion(Guid sessionId, Guid questionId, int position, string contentSnapshot, string rubricSnapshot)
    {
        DomainRules.RequiredId(sessionId, nameof(VivaSessionId)); DomainRules.RequiredId(questionId, nameof(QuestionId));
        if (position < 1) throw new DomainValidationException("Position must be positive.");
        var content = DomainRules.RequiredText(contentSnapshot, nameof(ContentSnapshot), 4000);
        var rubric = DomainRules.RequiredText(rubricSnapshot, nameof(RubricSnapshot));
        try { using var parsed = JsonDocument.Parse(rubric); if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException(); }
        catch (JsonException) { throw new DomainValidationException("Rubric snapshot must be a JSON object."); }
        VivaSessionId = sessionId; QuestionId = questionId; Position = position;
        ContentSnapshot = content; RubricSnapshot = rubric;
    }
}
