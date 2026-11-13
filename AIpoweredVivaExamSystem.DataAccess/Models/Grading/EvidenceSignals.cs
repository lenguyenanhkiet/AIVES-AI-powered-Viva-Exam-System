using AIpoweredVivaExamSystem.DataAccess.Enums;

namespace AIpoweredVivaExamSystem.DataAccess.Models.Grading;

public sealed record EvidenceSignals(double AnswerSeconds, int TurnLimitSeconds, int FollowUpCount, int AudioSeconds, int AnswerLength);
