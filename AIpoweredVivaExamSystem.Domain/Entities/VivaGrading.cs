using AIpoweredVivaExamSystem.Domain.Common;

namespace AIpoweredVivaExamSystem.Domain.Entities;

public sealed class AiGrading : AuditableEntity
{
    public Guid SessionQuestionId { get; private set; }
    public decimal AiScore { get; private set; }
    public decimal MaxScore { get; private set; }
    public string? AiComment { get; private set; }
    public string? Strengths { get; private set; }
    public string? Weaknesses { get; private set; }
    public string? MissingPoints { get; private set; }
    public int TotalResponseSeconds { get; private set; }
    public int WordsPerMinute { get; private set; }
    public string? FluencyNote { get; private set; }
    public string ModelName { get; private set; } = "";
    public string PromptVersion { get; private set; } = "v1";

    private readonly List<AiGradingCriterion> criteria = [];
    public IReadOnlyCollection<AiGradingCriterion> Criteria => criteria;

    private AiGrading() { }

    public AiGrading(
        Guid sessionQuestionId,
        decimal aiScore,
        decimal maxScore,
        string? aiComment,
        string? strengths,
        string? weaknesses,
        string? missingPoints,
        int totalResponseSeconds,
        int wordsPerMinute,
        string? fluencyNote,
        string modelName,
        string promptVersion = "v1")
    {
        DomainRules.RequiredId(sessionQuestionId, nameof(SessionQuestionId));
        SessionQuestionId = sessionQuestionId;
        Update(
            aiScore,
            maxScore,
            aiComment,
            strengths,
            weaknesses,
            missingPoints,
            totalResponseSeconds,
            wordsPerMinute,
            fluencyNote,
            modelName,
            promptVersion);
    }

    public void Update(
        decimal aiScore,
        decimal maxScore,
        string? aiComment,
        string? strengths,
        string? weaknesses,
        string? missingPoints,
        int totalResponseSeconds,
        int wordsPerMinute,
        string? fluencyNote,
        string modelName,
        string promptVersion = "v1")
    {
        if (maxScore <= 0 || maxScore > 999.99m)
            throw new DomainValidationException("MaxScore must be between 0.01 and 999.99.");
        var roundedMax = decimal.Round(maxScore, 2, MidpointRounding.AwayFromZero);
        var roundedScore = decimal.Round(aiScore, 2, MidpointRounding.AwayFromZero);
        if (roundedScore < 0 || roundedScore > roundedMax)
            throw new DomainValidationException($"AiScore must be between 0 and {roundedMax}.");
        if (totalResponseSeconds < 0)
            throw new DomainValidationException("TotalResponseSeconds cannot be negative.");
        if (wordsPerMinute < 0)
            throw new DomainValidationException("WordsPerMinute cannot be negative.");

        var validModel = DomainRules.RequiredText(modelName, nameof(ModelName), 100);
        var validVersion = DomainRules.RequiredText(promptVersion, nameof(PromptVersion), 50);

        MaxScore = roundedMax;
        AiScore = roundedScore;
        AiComment = NormalizeOptional(aiComment, nameof(AiComment), 4000);
        Strengths = NormalizeOptional(strengths, nameof(Strengths), 4000);
        Weaknesses = NormalizeOptional(weaknesses, nameof(Weaknesses), 4000);
        MissingPoints = NormalizeOptional(missingPoints, nameof(MissingPoints), 4000);
        TotalResponseSeconds = totalResponseSeconds;
        WordsPerMinute = wordsPerMinute;
        FluencyNote = NormalizeOptional(fluencyNote, nameof(FluencyNote), 1000);
        ModelName = validModel;
        PromptVersion = validVersion;
    }

    internal static string? NormalizeOptional(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new DomainValidationException($"{field} must not exceed {maxLength} characters.");
        return trimmed;
    }
}

public sealed class AiGradingCriterion : BaseEntity
{
    public Guid AiGradingId { get; private set; }
    public Guid? RubricCriterionId { get; private set; }
    public string CriterionName { get; private set; } = "";
    public decimal MaxScore { get; private set; }
    public decimal Score { get; private set; }
    public string? Evidence { get; private set; }
    public string? Comment { get; private set; }
    public int DisplayOrder { get; private set; }

    private AiGradingCriterion() { }

    public AiGradingCriterion(
        Guid aiGradingId,
        Guid? rubricCriterionId,
        string criterionName,
        decimal maxScore,
        decimal score,
        string? evidence,
        string? comment,
        int displayOrder)
    {
        DomainRules.RequiredId(aiGradingId, nameof(AiGradingId));
        var validName = DomainRules.RequiredText(criterionName, nameof(CriterionName), 255);
        if (maxScore <= 0 || maxScore > 999.99m)
            throw new DomainValidationException("Criterion MaxScore must be between 0.01 and 999.99.");
        var roundedMax = decimal.Round(maxScore, 2, MidpointRounding.AwayFromZero);
        var roundedScore = decimal.Round(score, 2, MidpointRounding.AwayFromZero);
        if (roundedScore < 0 || roundedScore > roundedMax)
            throw new DomainValidationException($"Criterion Score must be between 0 and {roundedMax}.");
        if (displayOrder < 1)
            throw new DomainValidationException("DisplayOrder must be at least 1.");

        AiGradingId = aiGradingId;
        RubricCriterionId = rubricCriterionId == Guid.Empty ? null : rubricCriterionId;
        CriterionName = validName;
        MaxScore = roundedMax;
        Score = roundedScore;
        Evidence = AiGrading.NormalizeOptional(evidence, nameof(Evidence), 2000);
        Comment = AiGrading.NormalizeOptional(comment, nameof(Comment), 2000);
        DisplayOrder = displayOrder;
    }
}

public sealed class TeacherReview : AuditableEntity
{
    public Guid SessionQuestionId { get; private set; }
    public Guid LecturerId { get; private set; }
    public decimal? AiScoreSnapshot { get; private set; }
    public decimal TeacherScore { get; private set; }
    public decimal FinalScore { get; private set; }
    public decimal MaxScore { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset ReviewedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    private TeacherReview() { }

    public TeacherReview(
        Guid sessionQuestionId,
        Guid lecturerId,
        decimal finalScore,
        decimal maxScore,
        decimal? aiScoreSnapshot,
        string? comment,
        DateTimeOffset reviewedAt)
    {
        DomainRules.RequiredId(sessionQuestionId, nameof(SessionQuestionId));
        SessionQuestionId = sessionQuestionId;
        UpdateReview(lecturerId, finalScore, maxScore, aiScoreSnapshot, comment, reviewedAt);
    }

    public void UpdateReview(
        Guid lecturerId,
        decimal finalScore,
        decimal maxScore,
        decimal? aiScoreSnapshot,
        string? comment,
        DateTimeOffset reviewedAt)
    {
        DomainRules.RequiredId(lecturerId, nameof(LecturerId));
        if (maxScore <= 0 || maxScore > 999.99m)
            throw new DomainValidationException("MaxScore must be between 0.01 and 999.99.");
        var roundedMax = decimal.Round(maxScore, 2, MidpointRounding.AwayFromZero);
        var roundedFinal = decimal.Round(finalScore, 2, MidpointRounding.AwayFromZero);
        if (roundedFinal < 0 || roundedFinal > roundedMax)
            throw new DomainValidationException($"Điểm giảng viên chốt phải nằm trong khoảng từ 0 đến {roundedMax}.");

        decimal? roundedSnapshot = null;
        if (aiScoreSnapshot.HasValue)
        {
            roundedSnapshot = decimal.Clamp(
                decimal.Round(aiScoreSnapshot.Value, 2, MidpointRounding.AwayFromZero),
                0m,
                roundedMax);
        }

        LecturerId = lecturerId;
        MaxScore = roundedMax;
        TeacherScore = roundedFinal;
        FinalScore = roundedFinal;
        AiScoreSnapshot = roundedSnapshot;
        Comment = AiGrading.NormalizeOptional(comment, nameof(Comment), 4000);
        ReviewedAt = reviewedAt;
    }
}
