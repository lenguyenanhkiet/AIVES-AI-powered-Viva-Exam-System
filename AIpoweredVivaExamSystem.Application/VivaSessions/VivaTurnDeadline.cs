namespace AIpoweredVivaExamSystem.Application.VivaSessions;

public static class VivaTurnDeadline
{
    public const int GraceSeconds = 2;
    public const string NoAnswerMarker = "[Hết thời gian: không có nội dung được ghi nhận; xem bản ghi âm nếu có.]";
    public const string TimeoutModelName = "server-timeout";

    public static bool HasExpired(DateTimeOffset deadline, DateTimeOffset receivedAt, bool timedOut) =>
        receivedAt >= deadline ||
        (timedOut && receivedAt >= deadline.AddSeconds(-GraceSeconds));
}
