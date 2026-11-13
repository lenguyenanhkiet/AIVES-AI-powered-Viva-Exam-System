using System.Text.Json;
using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.Business.Grading;
using AIpoweredVivaExamSystem.DataAccess.Models.Grading;
using AIpoweredVivaExamSystem.Business.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.Business.Integrations.Grading;
using AIpoweredVivaExamSystem.DataAccess.Context;
using AIpoweredVivaExamSystem.DataAccess.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;
namespace AIpoweredVivaExamSystem.Tests;

public sealed class GradingBoundsTests
{
    private sealed class MalformedProvider:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) {Content=new StringContent("{\"choices\":[]}")});
    }
    [Fact]
    public async Task Malformed_provider_envelope_is_rejected_as_a_retryable_suggestion_failure()
    {
        using var http=new HttpClient(new MalformedProvider());
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {["Ai:ApiKey"]="test-key"}).Build();
        var grader=new OpenAiRubricGrader(http,configuration);
        var context=new QuestionGradingContext("Question",null,[new(Guid.NewGuid(),"Concept",null,null,10,1)],[new("Question","Answer")]);
        await Assert.ThrowsAsync<ResourceConflictException>(()=>grader.SuggestAsync(context,default));
    }
    [Fact]
    public void Suggestions_cannot_invent_criteria_or_exceed_the_rubric_bounds()
    {
        var a=new GradingCriterion(Guid.NewGuid(),"Concept",null,null,4,1);
        var b=new GradingCriterion(Guid.NewGuid(),"Application",null,null,6,2);
        var context=new QuestionGradingContext("Question",null,[a,b],[new("Question","A partial answer")]);
        var suggestion=new QuestionSuggestion([new(a.Id,99,"Identified a concept."),new(b.Id,-5,"No example.")],"Concept","No example","Application","test-model");
        var valid=GradingRules.ValidateSuggestion(context,suggestion);
        Assert.Equal(4,valid.Criteria.Single(c=>c.CriterionId==a.Id).Score);
        Assert.Equal(0,valid.Criteria.Single(c=>c.CriterionId==b.Id).Score);
        Assert.Throws<ResourceConflictException>(()=>GradingRules.ValidateSuggestion(context,
            suggestion with {Criteria=[new(Guid.NewGuid(),4,"Forged criterion"),new(b.Id,6,"Example")]}));
        var unanswered=GradingRules.ValidateSuggestion(context with {Conversation=[new("Question",null)]},suggestion);
        Assert.All(unanswered.Criteria,c=>Assert.Equal(0,c.Score));
    }
}

[Collection("SQL Server")]
public sealed class GradingSqlTests(SqlServerFixture fixture)
{
    private sealed record Actor(Guid Id,bool IsAdministrator=false,bool IsLecturer=false):ICurrentUser;
    private sealed class Notifier:IExamNotifier {public Task NotifyChangedAsync(Guid id,CancellationToken ct)=>Task.CompletedTask;}
    private sealed class Grader(Func<QuestionGradingContext,QuestionSuggestion> grade):IAiRubricGrader
    {public Task<QuestionSuggestion> SuggestAsync(QuestionGradingContext context,CancellationToken ct)=>Task.FromResult(grade(context));}
    private sealed class Audio:IAnswerAudioStorage
    {public Task<string> SaveAudioAsync(Guid session,Guid turn,byte[] bytes,string mime,CancellationToken ct)=>Task.FromResult("test.webm");}
    private sealed class Examiner:IVivaAiExaminer
    {public Task<ExaminerDecision> EvaluateAndFollowUpAsync(ExaminerEvaluationContext c,CancellationToken ct)=>Task.FromResult(new ExaminerDecision(true,null,null,null,"test"));}
    private sealed class Transcriber:ISpeechTranscriber
    {public Task<string> TranscribeAsync(byte[] b,string f,string l,string? h,CancellationToken ct)=>Task.FromResult("Answer");}
    private sealed class FailingResultNotifier(ApplicationDbContext db):IStudentResultNotifier
    {
        public int Calls;
        public Task NotifyChangedAsync(Guid sessionId,CancellationToken ct)
        {
            Assert.Null(db.Database.CurrentTransaction);
            Calls++;
            throw new IOException("Notification transport unavailable.");
        }
    }
    private ApplicationDbContext Db(Actor actor)=>new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(fixture.ConnectionString).Options,actor);
    private static GradingService Service(ApplicationDbContext db,Actor actor,IAiRubricGrader grader,IStudentResultNotifier? notifier=null)=>new(new GradingStore(db,new NotifyingExamUnitOfWork(new ExamUnitOfWork(db), new Notifier())),
        new ExamAccessGuard(new ExamFoundationStore(db),actor),actor,grader,notifier);
    private static IVivaInterviewService StudentService(ApplicationDbContext db,Actor actor,ISpeechTranscriber? transcriber=null)
    {
        var uow=new NotifyingExamUnitOfWork(new ExamUnitOfWork(db), new Notifier());
        return new VivaInterviewService(new VivaSessionStore(db,uow,actor),new VivaInterviewStore(db,uow,new Audio(),actor),
            transcriber??new Transcriber(),new ExamAccessGuard(new ExamFoundationStore(db),actor),actor,new Examiner());
    }
    private async Task<(ExamDemo Demo,VivaSession Session,SessionQuestion Question,StudentAnswer Answer,AnswerAudio Audio)> SeedAsync(bool completed=true,bool expired=false)
    {
        await using var db=fixture.CreateContext(); var demo=await ExamDemo.SeedAsync(db);
        db.Entry(demo.Exam).Property(e=>e.StartAt).CurrentValue=DateTimeOffset.UtcNow.AddHours(-2);
        db.Entry(demo.Exam).Property(e=>e.EndAt).CurrentValue=DateTimeOffset.UtcNow.AddHours(2);
        db.Entry(demo.Exam).Property(e=>e.Status).CurrentValue=ExamStatus.Ongoing;
        var start=DateTimeOffset.UtcNow.AddMinutes(expired?-30:-5); demo.Enrollment.Schedule(start);
        var session=new VivaSession(demo.Enrollment.Id,start.AddSeconds(1));
        var rubric=new Rubric(demo.Question.Id,"SECRET rubric",null,[new(null,"Concept",null,"SECRET expected concept",4,1),new(null,"Example",null,"SECRET example",6,2)]);
        var question=new SessionQuestion(session.Id,demo.Question.Id,1,demo.Question.Content,JsonSerializer.Serialize(new {rubric.Criteria,ExpectedAnswer="SECRET expected answer"}));
        var answer=new StudentAnswer(question.Id,AnswerType.Main,0,question.ContentSnapshot,start.AddSeconds(1));
        if(completed) {answer.SubmitResponse("The student explained the concept.",start.AddMinutes(1));session.Complete(start.AddMinutes(2));}
        var audio=new AnswerAudio(answer.Id,"viva-audios/test.webm","audio/webm",3,"abc",10);
        db.AddRange(session,rubric,question,answer,audio);await db.SaveChangesAsync();return(demo,session,question,answer,audio);
    }
    private static GradeInput Input(SessionReview review,decimal score=2)=>new() {Version=review.Version,OverallComment="Lecturer feedback",
        Questions=review.Questions.Select(q=>new QuestionGradeInput {SessionQuestionId=q.SessionQuestionId,Comment="Question feedback",
            Criteria=q.Criteria.Select(c=>new CriterionGradeInput {CriterionId=c.Id,Score=Math.Min(score,c.MaxScore)}).ToList()}).ToList()};

    [Fact]
    public async Task Lecturer_controls_finalisation_and_regeneration_never_overwrites_the_draft()
    {
        var seed=await SeedAsync();var actor=new Actor(seed.Demo.Lecturer.Id,IsLecturer:true);
        await using var db=Db(actor);var offered=1m;
        var notifier=new FailingResultNotifier(db);
        var service=Service(db,actor,new Grader(c=>{
            Assert.Null(db.Database.CurrentTransaction);
            return new(c.Criteria.Select(r=>new SuggestedCriterion(r.Id,offered,"Supported by the explanation.")).ToArray(),"Concept","Example absent","Example","test-model");
        }),notifier);
        var review=await service.ReviewAsync(seed.Session.Id,default);
        await service.SuggestAsync(seed.Session.Id,seed.Question.Id,false,default);
        review=await service.ReviewAsync(seed.Session.Id,default);
        Assert.NotNull(review.SuggestedTotal);Assert.Null(review.LecturerTotal);Assert.Null(review.FinalisedAt);
        var student=new Actor(seed.Demo.Student.Id);
        await using var studentDb=Db(student);var studentService=StudentService(studentDb,student);
        Assert.Null((await studentService.GetStateAsync(seed.Session.Id,default)).Result);
        var input=Input(review);
        await service.SaveAsync(seed.Session.Id,input,false,default);
        offered=3;
        await service.SuggestAsync(seed.Session.Id,seed.Question.Id,true,default);
        review=await service.ReviewAsync(seed.Session.Id,default);
        Assert.All(review.Questions.SelectMany(q=>q.Criteria),c=>Assert.Equal(2,c.LecturerScore));
        Assert.All(review.Questions.SelectMany(q=>q.Criteria),c=>Assert.Equal(3,c.SuggestedScore));
        Assert.Null((await studentService.GetStateAsync(seed.Session.Id,default)).Result);
        await Assert.ThrowsAsync<ResourceConflictException>(()=>service.SaveAsync(seed.Session.Id,input,false,default));
        Assert.Equal(0,notifier.Calls);
        await service.SaveAsync(seed.Session.Id,Input(review),true,default);
        var result=(await studentService.GetStateAsync(seed.Session.Id,default)).Result;
        Assert.NotNull(result);Assert.Equal(4,result.Total);
        Assert.DoesNotContain("SECRET",JsonSerializer.Serialize(result));
        Assert.DoesNotContain("Suggested",JsonSerializer.Serialize(result));
        review=await service.ReviewAsync(seed.Session.Id,default);
        await service.ReopenAsync(seed.Session.Id,review.Version,default);
        Assert.Null((await studentService.GetStateAsync(seed.Session.Id,default)).Result);
        review=await service.ReviewAsync(seed.Session.Id,default);
        await service.SaveAsync(seed.Session.Id,Input(review,3),true,default);
        Assert.Equal(6,(await studentService.GetStateAsync(seed.Session.Id,default)).Result!.Total);
        Assert.Equal(3,notifier.Calls); // finalise, reopen, re-finalise all survive failed delivery
    }

    [Fact]
    public async Task Unauthorised_lecturers_and_students_cannot_read_or_change_the_grade_or_audio()
    {
        var seed=await SeedAsync();var wrongLecturer=new Actor(Guid.NewGuid(),IsLecturer:true);
        await using var db=Db(wrongLecturer);
        var service=Service(db,wrongLecturer,new Grader(_=>throw new InvalidOperationException("Must not call AI.")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.ReviewAsync(seed.Session.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveAsync(seed.Session.Id,new(),false,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.SaveAsync(seed.Session.Id,new(),true,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>service.AudioAsync(seed.Audio.Id,default));
        var otherStudent=new Actor(Guid.NewGuid());await using var otherDb=Db(otherStudent);
        await Assert.ThrowsAsync<ResourceNotFoundException>(()=>StudentService(otherDb,otherStudent).GetStateAsync(seed.Session.Id,default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>Service(otherDb,otherStudent,new Grader(_=>throw new InvalidOperationException())).AudioAsync(seed.Audio.Id,default));
    }

    [Fact]
    public async Task Lecturer_can_grade_manually_after_provider_failure_and_retry_the_suggestion()
    {
        var seed=await SeedAsync();var actor=new Actor(seed.Demo.Lecturer.Id,IsLecturer:true);await using var db=Db(actor);
        var unavailable=true;
        var service=Service(db,actor,new Grader(c=>unavailable?throw new ResourceConflictException("Unavailable"):
            new(c.Criteria.Select(r=>new SuggestedCriterion(r.Id,1,"Partial explanation.")).ToArray(),"Concept","Example","Example","test")));
        var question=await service.SuggestAsync(seed.Session.Id,seed.Question.Id,false,default);
        Assert.Null(question.SuggestedTotal);
        var review=await service.ReviewAsync(seed.Session.Id,default);
        await service.SaveAsync(seed.Session.Id,Input(review),false,default);
        unavailable=false;
        question=await service.SuggestAsync(seed.Session.Id,seed.Question.Id,true,default);
        Assert.NotNull(question.SuggestedTotal);Assert.All(question.Criteria,c=>Assert.Equal(2,c.LecturerScore));
    }

    [Fact]
    public async Task Reading_the_lecturer_list_persistently_closes_an_abandoned_expired_session()
    {
        var seed=await SeedAsync(completed:false,expired:true);var actor=new Actor(seed.Demo.Lecturer.Id,IsLecturer:true);await using var db=Db(actor);
        var service=Service(db,actor,new Grader(_=>throw new InvalidOperationException("No answer must not call AI.")));
        var list=await service.ListAsync(seed.Demo.Exam.Id,default);
        Assert.NotNull(Assert.Single(list.Sessions).CompletedAt);
        var question=await service.SuggestAsync(seed.Session.Id,seed.Question.Id,false,default);
        Assert.Equal(0,question.SuggestedTotal);Assert.True(Assert.Single(question.Turns).TimedOut);
        Assert.Null(Assert.Single(question.Turns).Transcript);
        await using var verify=fixture.CreateContext();
        Assert.NotNull((await verify.VivaSessions.SingleAsync(s=>s.Id==seed.Session.Id)).CompletedAt);
        Assert.NotNull((await verify.StudentAnswers.SingleAsync(t=>t.Id==seed.Answer.Id)).AnsweredAt);
    }

    private sealed class Clock(DateTimeOffset now):TimeProvider
    { public override DateTimeOffset GetUtcNow()=>now; }
    private sealed class PausedTranscriber:ISpeechTranscriber
    {
        public TaskCompletionSource Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TranscribeAsync(byte[] b,string f,string l,string? h,CancellationToken ct)
        {
            Entered.TrySetResult(); await Release.Task.WaitAsync(ct); return "An answer received before the deadline.";
        }
    }
    [Fact]
    public async Task Expiry_cannot_discard_an_on_time_answer_while_transcription_is_in_flight()
    {
        var seed=await SeedAsync(completed:false);
        var student=new Actor(seed.Demo.Student.Id);
        await using var studentDb=Db(student);
        // The active turn is recent even though this session started several minutes ago.
        var turn=await studentDb.StudentAnswers.IgnoreQueryFilters().SingleAsync(t=>t.Id==seed.Answer.Id);
        studentDb.Entry(turn).Property(t=>t.AskedAt).CurrentValue=DateTimeOffset.UtcNow;
        await studentDb.SaveChangesAsync();
        var transcriber=new PausedTranscriber();var studentService=StudentService(studentDb,student,transcriber);
        var state=await studentService.GetStateAsync(seed.Session.Id,default);
        var submit=studentService.SubmitAnswerAsync(new(state.SessionId,turn.Id,null,[1,2,3],"audio/webm"),default);
        try
        {
            await transcriber.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var lecturer=new Actor(seed.Demo.Lecturer.Id,IsLecturer:true);
            await using var lecturerDb=Db(lecturer);
            var store=new GradingStore(lecturerDb,new NotifyingExamUnitOfWork(new ExamUnitOfWork(lecturerDb), new Notifier()),
                new Clock(state.SlotEndAt.AddSeconds(VivaTurnDeadline.GraceSeconds+1)));
            var list=await store.ListAsync(seed.Demo.Exam.Id,default);
            Assert.Null(Assert.Single(list.Sessions).CompletedAt);
        }
        finally {transcriber.Release.TrySetResult();await submit;}
        await using var verify=fixture.CreateContext();
        Assert.Equal("An answer received before the deadline.",(await verify.StudentAnswers.SingleAsync(t=>t.Id==turn.Id)).Transcript);
    }
}
