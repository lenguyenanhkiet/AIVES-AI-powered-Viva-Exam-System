using System.Collections.Concurrent;
using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIpoweredVivaExamSystem.Web.Realtime;

public sealed record ExamSubscription(string ConnectionId, Guid UserId, Guid ExamId);
public sealed record ExamListSubscription(string ConnectionId, Guid UserId);
public sealed class ExamConnections
{
    public ConcurrentDictionary<(string, Guid), ExamSubscription> Items { get; } = new();
    public ConcurrentDictionary<string, ExamListSubscription> ListWatchers { get; } = new();
    public ConcurrentDictionary<string, ExamListSubscription> StudentScheduleWatchers { get; } = new();
}

public sealed class ExamRealtimeAccess(IRealtimeUserReader users, IExamAccessStore exams)
{
    public Task<bool> CanManageAsync(Guid userId, Guid examId, CancellationToken ct) =>
        CanManageCoreAsync(userId, examId, false, ct);

    public Task<bool> CanManageListAsync(Guid userId, Guid examId, CancellationToken ct) =>
        CanManageCoreAsync(userId, examId, true, ct);

    public async Task<bool> CanWatchStudentScheduleAsync(Guid userId, CancellationToken ct) =>
        await users.FindAsync(userId, ct) is { IsDeleted: false, Status: UserStatus.Active, Role: UserRole.STUDENT };

    public async Task<bool> CanReceiveStudentScheduleChangeAsync(Guid userId, Guid examId, CancellationToken ct) =>
        await CanWatchStudentScheduleAsync(userId, ct) && await exams.IsEnrolledAsync(examId, userId, ct);

    private async Task<bool> CanManageCoreAsync(Guid userId, Guid examId, bool includeDeleted, CancellationToken ct)
    {
        var user = await users.FindAsync(userId, ct);
        if (user is null || user.IsDeleted || user.Status != UserStatus.Active) return false;
        var exam = includeDeleted
            ? await exams.FindExamForNotificationAsync(examId, ct)
            : await exams.FindExamAsync(examId, ct);
        return exam is not null && (user.Role == UserRole.ADMIN ||
            (user.Role == UserRole.LECTURER && exam.LecturerId == userId && await exams.IsAssignedAsync(userId, exam.SubjectId, ct)));
    }
}

[Authorize(Roles = "ADMIN,LECTURER,STUDENT")]
public sealed class ExamHub(ExamRealtimeAccess access, ExamConnections connections) : Hub
{
    public static string Group(Guid examId) => $"exam:{examId}";
    [Authorize(Roles = "ADMIN,LECTURER")]
    public async Task JoinExam(Guid examId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await access.CanManageAsync(userId, examId, Context.ConnectionAborted))
            throw new HubException("You cannot manage this exam.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(examId));
        connections.Items[(Context.ConnectionId, examId)] = new(Context.ConnectionId, userId, examId);
    }
    [Authorize(Roles = "ADMIN,LECTURER")]
    public Task JoinExamList()
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            throw new HubException("You cannot manage exams.");
        connections.ListWatchers[Context.ConnectionId] = new(Context.ConnectionId, userId);
        return Task.CompletedTask;
    }
    [Authorize(Roles = "STUDENT")]
    public async Task JoinStudentSchedule()
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await access.CanWatchStudentScheduleAsync(userId, Context.ConnectionAborted))
            throw new HubException("You cannot watch the student schedule.");

        connections.StudentScheduleWatchers[Context.ConnectionId] = new(Context.ConnectionId, userId);
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var key in connections.Items.Keys.Where(k => k.Item1 == Context.ConnectionId)) connections.Items.TryRemove(key, out _);
        connections.ListWatchers.TryRemove(Context.ConnectionId, out _);
        connections.StudentScheduleWatchers.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class SignalRExamNotifier(IHubContext<ExamHub> hub, ExamConnections connections, ExamRealtimeAccess access) : IExamNotifier
{
    public async Task NotifyChangedAsync(Guid examId, CancellationToken cancellationToken)
    {
        // Recheck SQL permissions before every delivery, including connections established before revocation.
        // Deliver to checked connections to avoid a join racing with group revalidation.
        foreach (var item in connections.Items.Values.Where(s => s.ExamId == examId))
        {
            if (await access.CanManageAsync(item.UserId, examId, cancellationToken))
                await hub.Clients.Client(item.ConnectionId).SendAsync("ExamChanged", examId, cancellationToken);
            else
            {
                connections.Items.TryRemove((item.ConnectionId, examId), out _);
                await hub.Groups.RemoveFromGroupAsync(item.ConnectionId, ExamHub.Group(examId), cancellationToken);
            }
        }
        foreach (var item in connections.ListWatchers.Values)
        {
            if (await access.CanManageListAsync(item.UserId, examId, cancellationToken))
                await hub.Clients.Client(item.ConnectionId).SendAsync("ExamListChanged", examId, cancellationToken);
        }
        foreach (var item in connections.StudentScheduleWatchers.Values)
        {
            if (await access.CanReceiveStudentScheduleChangeAsync(item.UserId, examId, cancellationToken))
                await hub.Clients.Client(item.ConnectionId).SendAsync("StudentScheduleChanged", cancellationToken);
        }
    }
}
