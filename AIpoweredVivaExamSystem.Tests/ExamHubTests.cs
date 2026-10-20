using System.Security.Claims;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Web.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class ExamHubTests
{
    private sealed class Users(User user) : ILoginUserRepository
    {
        public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<User?>(id == user.Id ? user : null);
        public Task<User?> FindByEmailAsync(string email, CancellationToken ct) => Task.FromResult<User?>(null);
    }
    private sealed class Exams(Guid owner) : IExamAccessStore
    {
        public bool Assigned = true;
        public Task<ExamAccess?> FindExamAsync(Guid id, CancellationToken ct) => Task.FromResult<ExamAccess?>(new(id, Guid.NewGuid(), owner));
        public Task<bool> IsAssignedAsync(Guid lecturer, Guid subject, CancellationToken ct) => Task.FromResult(Assigned);
        public Task<ExamStudentAccess?> FindEnrollmentAsync(Guid id, CancellationToken ct) => Task.FromResult<ExamStudentAccess?>(null);
        public Task<bool> IsEnrolledAsync(Guid exam, Guid student, CancellationToken ct) => Task.FromResult(false);
    }
    private sealed class Caller(Guid userId) : HubCallerContext
    {
        public override string ConnectionId => "test-connection";
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
    private sealed class FakeClients : IHubClients, IClientProxy
    {
        public int Sends;
        public Task SendCoreAsync(string method, object?[] args, CancellationToken ct = default)
        { Sends++; Assert.Equal("ExamChanged", method); Assert.IsType<Guid>(Assert.Single(args)); return Task.CompletedTask; }
        public IClientProxy All => this;
        public IClientProxy AllExcept(IReadOnlyList<string> excluded) => this;
        public IClientProxy Client(string id) => this;
        public IClientProxy Clients(IReadOnlyList<string> ids) => this;
        public IClientProxy Group(string name) => this;
        public IClientProxy GroupExcept(string name, IReadOnlyList<string> excluded) => this;
        public IClientProxy Groups(IReadOnlyList<string> names) => this;
        public IClientProxy User(string id) => this;
        public IClientProxy Users(IReadOnlyList<string> ids) => this;
    }
    private sealed class HubContext(FakeClients clients, Groups groups) : IHubContext<ExamHub>
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => groups;
    }

    [Fact]
    public async Task JoinReconnectRevocationAndDisconnectAreAuthorized()
    {
        var user = new User("hub@example.test", "unused", "Lecturer", UserStatus.Active);
        user.AssignRole(UserRole.LECTURER);
        var exams = new Exams(user.Id);
        var access = new ExamRealtimeAccess(new Users(user), exams);
        var registry = new ExamConnections();
        var groups = new Groups();
        var clients = new FakeClients();
        var hub = new ExamHub(access, registry) { Context = new Caller(user.Id), Groups = groups };
        var id = Guid.NewGuid();
        await hub.JoinExam(id);
        await hub.JoinExam(id); // reconnect/rejoin must not duplicate subscriptions
        Assert.Single(registry.Items);
        var notifier = new SignalRExamNotifier(new HubContext(clients, groups), registry, access);
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(1, clients.Sends);
        exams.Assigned = false;
        await notifier.NotifyChangedAsync(id, default);
        Assert.Equal(1, clients.Sends);
        Assert.Equal(1, groups.Removes);
        Assert.Empty(registry.Items);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        exams.Assigned = true;
        await hub.JoinExam(id);
        await hub.OnDisconnectedAsync(null);
        Assert.Empty(registry.Items);
        user.AssignRole(UserRole.STUDENT);
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
        user.AssignRole(UserRole.LECTURER);
        user.Lock();
        await Assert.ThrowsAsync<HubException>(() => hub.JoinExam(id));
    }
}
