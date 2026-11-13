using AIpoweredVivaExamSystem.Business.Grading;
using System.Security.Claims;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.DataAccess.Repositories;
using AIpoweredVivaExamSystem.Presentation.Realtime;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

[Collection("SQL Server")]
public sealed class StudentResultHubTests(SqlServerFixture fixture)
{
    private sealed class Caller(Guid studentId,string connectionId):HubCallerContext
    {
        public override string ConnectionId=>connectionId;
        public override string? UserIdentifier=>studentId.ToString();
        public override ClaimsPrincipal User=>new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,studentId.ToString())],"test"));
        public override IDictionary<object,object?> Items {get;}=new Dictionary<object,object?>();
        public override IFeatureCollection Features {get;}=new FeatureCollection();
        public override CancellationToken ConnectionAborted=>default;
        public override void Abort() { }
    }
    private sealed class FakeClients:IHubClients
    {
        public List<(string ConnectionId,Guid SessionId)> Messages {get;}=[];
        private sealed class Proxy(FakeClients owner,string connectionId):IClientProxy
        {
            public Task SendCoreAsync(string method,object?[] args,CancellationToken ct=default)
            {
                // The invalidation carries only the already-owned session ID, never a grade.
                owner.Messages.Add((connectionId,Assert.IsType<Guid>(Assert.Single(args))));
                return Task.CompletedTask;
            }
        }
        public IClientProxy Client(string connectionId)=>new Proxy(this,connectionId);
        public IClientProxy All=>throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy Group(string name)=>throw new NotSupportedException();
        public IClientProxy GroupExcept(string name,IReadOnlyList<string> ids)=>throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> names)=>throw new NotSupportedException();
        public IClientProxy User(string userId)=>throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds)=>throw new NotSupportedException();
    }
    private sealed class Context(FakeClients clients):IHubContext<StudentResultHub>
    {
        public IHubClients Clients=>clients;
        public IGroupManager Groups=>throw new NotSupportedException();
    }
    [Fact]
    public async Task Only_the_current_student_owner_can_join_and_receive_an_invalidation()
    {
        await using var db=fixture.CreateContext();var demo=await ExamDemo.SeedAsync(db);
        var other=new User(Guid.NewGuid()+"@aives.test","unused","Other student",UserStatus.Active);
        var session=new VivaSession(demo.Enrollment.Id,DateTimeOffset.UtcNow);
        session.Complete(DateTimeOffset.UtcNow);db.AddRange(other,session);await db.SaveChangesAsync();
        var access=new StudentResultAccessService(new StudentResultAccess(db));var registry=new StudentResultConnections();var clients=new FakeClients();
        var ownerHub=new StudentResultHub(access,registry){Context=new Caller(demo.Student.Id,"owner")};
        var otherHub=new StudentResultHub(access,registry){Context=new Caller(other.Id,"other")};
        await Assert.ThrowsAsync<HubException>(()=>otherHub.JoinSession(session.Id));
        Assert.Empty(registry.Items);
        await ownerHub.JoinSession(session.Id);await ownerHub.JoinSession(session.Id);
        Assert.Single(registry.Items);
        var notifier=new SignalRStudentResultNotifier(new Context(clients),registry,access,NullLogger<SignalRStudentResultNotifier>.Instance);
        await notifier.NotifyChangedAsync(session.Id,default);
        Assert.Equal(("owner",session.Id),Assert.Single(clients.Messages));
        // The cookie may still claim STUDENT; delivery must use the current SQL role.
        demo.Student.AssignRole(UserRole.LECTURER);await db.SaveChangesAsync();clients.Messages.Clear();
        await notifier.NotifyChangedAsync(session.Id,default);
        Assert.Empty(clients.Messages);Assert.Empty(registry.Items);
        await Assert.ThrowsAsync<HubException>(()=>ownerHub.JoinSession(session.Id));
    }
}
