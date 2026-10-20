using System.Collections.Concurrent;
using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIpoweredVivaExamSystem.Web.Realtime;

public sealed record ExamSubscription(string ConnectionId, Guid UserId, Guid ExamId);
public sealed class ExamConnections
{
    public ConcurrentDictionary<(string, Guid), ExamSubscription> Items { get; } = new();
}

public sealed class ExamRealtimeAccess(ILoginUserRepository users, IExamAccessStore exams)
{
    public async Task<bool> CanManageAsync(Guid userId, Guid examId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId, ct);
        if (user is null || user.DeletedAt != null || user.Status != UserStatus.Active) return false;
        var exam = await exams.FindExamAsync(examId, ct);
        return exam is not null && (user.Role == UserRole.ADMIN ||
            (user.Role == UserRole.LECTURER && exam.LecturerId == userId && await exams.IsAssignedAsync(userId, exam.SubjectId, ct)));
    }
}

[Authorize(Roles = "ADMIN,LECTURER")]
public sealed class ExamHub(ExamRealtimeAccess access, ExamConnections connections) : Hub
{
    public static string Group(Guid examId) => $"exam:{examId}";
    public async Task JoinExam(Guid examId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !await access.CanManageAsync(userId, examId, Context.ConnectionAborted))
            throw new HubException("You cannot manage this exam.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(examId));
        connections.Items[(Context.ConnectionId, examId)] = new(Context.ConnectionId, userId, examId);
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var key in connections.Items.Keys.Where(k => k.Item1 == Context.ConnectionId)) connections.Items.TryRemove(key, out _);
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
    }
}
