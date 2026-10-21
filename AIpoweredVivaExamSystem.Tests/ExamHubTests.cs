using System.Security.Claims;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Repositories;
using AIpoweredVivaExamSystem.Web.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class ExamHubTests(SqlServerFixture fixture)
{
    private sealed class Caller(Guid userId, string connectionId = "test-connection") : HubCallerContext
    {
        public override string ConnectionId => connectionId;
        public override string? UserIdentifier => userId.ToString();
        public override ClaimsPrincipal User => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => default;
        public override void Abort() { }
    }
    private sealed class Groups : IGroupManager
    {
        public int Adds, Removes;
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken ct = default) { Adds++; Assert.StartsWith("exam:", groupName); return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken ct = default) { Removes++; return Task.CompletedTask; }
    }
    private sealed class FakeClients : IHubClients
    {
        public List<(string ConnectionId, string Method, Guid ExamId)> Messages { get; } = [];
        private sealed class ClientProxy(FakeClients clients, string connectionId) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
            {
                clients.Messages.Add((connectionId, method, Assert.IsType<Guid>(Assert.Single(args))));
                return Task.CompletedTask;
            }
        }
        public IClientProxy Client(string id) => new ClientProxy(this, id);
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excluded) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> ids) => throw new NotSupportedException();
        public IClientProxy Group(string name) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string name, IReadOnlyList<string> excluded) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> names) => throw new NotSupportedException();
        public IClientProxy User(string id) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> ids) => throw new NotSupportedException();
    }
    private sealed class HubContext(FakeClients clients, Groups groups) : IHubContext<ExamHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => groups;
    }

    [Fact]
    public async Task JoinReconnectRevocationAndDisconnectAreAuthorized()
    {
        await using var db = fixture.CreateContext();
        var demo = await ExamDemo.SeedAsync(db);
        var user = demo.Lecturer;
        var other = new User(Guid.NewGuid() + "@example.test", "unused", "Other lecturer", UserStatus.Active);
        other.AssignRole(UserRole.LECTURER);
        var admin = new User(Guid.NewGuid() + "@example.test", "unused", "Admin", UserStatus.Active);
        admin.AssignRole(UserRole.ADMIN);
        // Assignment alone must not allow another lecturer to receive the owner's exam events.
        db.AddRange(other, admin, new LecturerSubject(other.Id, demo.Subject.Id));
        await db.SaveChangesAsync();
        var access = new ExamRealtimeAccess(new LoginUserRepository(db), new ExamFoundationStore(db));
        var registry = new ExamConnections();
        var groups = new Groups();
        var clients = new FakeClients();
        var hub = new ExamHub(access, registry) { Context = new Caller(user.Id), Groups = groups };
        var otherHub = new ExamHub(access, registry) { Context = new Caller(other.Id, "other-connection"), Groups = groups };
        var adminHub = new ExamHub(access, registry) { Context = new Caller(admin.Id, "admin-connection"), Groups = groups };
        var id = demo.Exam.Id;
        await hub.JoinExamList();
        await hub.JoinExamList();
        await otherHub.JoinExamList();
        await adminHub.JoinExamList();
        Assert.Equal(3, registry.ListWatchers.Count);
        await hub.JoinExam(id);
        await hub.JoinExam(id); // reconnect/rejoin must not duplicate subscriptions
        Assert.Single(registry.Items);
        var notifier = new SignalRExamNotifier(new HubContext(clients, groups), registry, access);
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(3, clients.Messages.Count);
        Assert.Contains(("test-connection", "ExamChanged", id), clients.Messages);
        Assert.Equal(new[] { "admin-connection", "test-connection" }, clients.Messages
            .Where(m => m.Method == "ExamListChanged").Select(m => m.ConnectionId).Order().ToArray());
        Assert.All(clients.Messages, message => Assert.Equal(id, message.ExamId));
        clients.Messages.Clear();
        var assignment = await db.LecturerSubjects.SingleAsync(a => a.LecturerId == user.Id && a.SubjectId == demo.Subject.Id);
        db.Remove(assignment);
        await db.SaveChangesAsync();
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(("admin-connection", "ExamListChanged", id), Assert.Single(clients.Messages));
        Assert.Equal(1, groups.Removes);
        Assert.Empty(registry.Items);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        db.Add(new LecturerSubject(user.Id, demo.Subject.Id));
        await db.SaveChangesAsync();
        await hub.JoinExam(id);
        await hub.OnDisconnectedAsync(null);
        Assert.Empty(registry.Items);
        Assert.False(registry.ListWatchers.ContainsKey("test-connection"));
        await hub.JoinExam(id);
        await hub.JoinExamList();
        user.AssignRole(UserRole.STUDENT);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        user.AssignRole(UserRole.LECTURER);
        user.Lock();
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        user.Activate();
        new ExamRepository(db).Remove(demo.Exam);
        await db.SaveChangesAsync();
        clients.Messages.Clear();
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(new[] { ("admin-connection", "ExamListChanged", id), ("test-connection", "ExamListChanged", id) },
            clients.Messages.OrderBy(m => m.ConnectionId).ToArray());
        Assert.Empty(registry.Items);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        await hub.OnDisconnectedAsync(null);
        Assert.False(registry.ListWatchers.ContainsKey("test-connection"));
        clients.Messages.Clear();
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(("admin-connection", "ExamListChanged", id), Assert.Single(clients.Messages));
        await otherHub.OnDisconnectedAsync(null);
        await adminHub.OnDisconnectedAsync(null);
        Assert.Empty(registry.ListWatchers);
    }
}
