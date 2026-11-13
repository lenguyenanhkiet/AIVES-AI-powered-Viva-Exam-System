namespace AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;

public static class VivaTurnDeadline
{
    public const int GraceSeconds = 2;
    public const string NoAnswerMarker = "[Hết thời gian: không có nội dung được ghi nhận; xem bản ghi âm nếu có.]";
    public const string TimeoutModelName = "server-timeout";

    public static bool HasExpired(DateTimeOffset deadline, DateTimeOffset receivedAt, bool timedOut) =>
        receivedAt >= deadline ||
        (timedOut && receivedAt >= deadline.AddSeconds(-GraceSeconds));
}

public static class VivaTiming
{
    public static int CalculateTurnLimitSeconds(int durationMinutes, int mainQuestions, int maxFollowUps)
    {
        var totalPossibleTurns = Math.Max(1, mainQuestions * (1 + Math.Max(0, maxFollowUps)));
        var rawSeconds = (Math.Max(1, durationMinutes) * 60) / totalPossibleTurns;
        return Math.Clamp(rawSeconds, 30, 300);
    }
}
