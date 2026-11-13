using System.Collections.Concurrent;
using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Grading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIpoweredVivaExamSystem.Web.Realtime;

public sealed record StudentResultSubscription(string ConnectionId, Guid StudentId, Guid SessionId);
public sealed class StudentResultConnections
{
    public ConcurrentDictionary<string, StudentResultSubscription> Items { get; } = new();
}

[Authorize(Roles = "STUDENT")]
public sealed class StudentResultHub(IStudentResultAccess access, StudentResultConnections connections) : Hub
{
    public async Task JoinSession(Guid sessionId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var studentId) ||
            !await access.CanReadAsync(studentId, sessionId, Context.ConnectionAborted))
            throw new HubException("Bạn không được xem kết quả của phiên thi này.");
        connections.Items[Context.ConnectionId] = new(Context.ConnectionId, studentId, sessionId);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Items.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }
}

public sealed class SignalRStudentResultNotifier(IHubContext<StudentResultHub> hub, StudentResultConnections connections,
    IStudentResultAccess access, ILogger<SignalRStudentResultNotifier> logger) : IStudentResultNotifier
{
    public async Task NotifyChangedAsync(Guid sessionId, CancellationToken ct)
    {
        // Check current SQL ownership/account permissions for each recipient; never broadcast
        // grades or send to a group whose membership may have been revoked since joining.
        foreach (var item in connections.Items.Values.Where(s => s.SessionId == sessionId))
        {
            try
            {
                if (await access.CanReadAsync(item.StudentId, sessionId, ct))
                    await hub.Clients.Client(item.ConnectionId).SendAsync("StudentResultChanged", sessionId, ct);
                else
                    connections.Items.TryRemove(item.ConnectionId, out _);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Student result delivery failed for session {SessionId}", sessionId);
            }
        }
    }
}
