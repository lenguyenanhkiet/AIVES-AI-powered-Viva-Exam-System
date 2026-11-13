using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIpoweredVivaExamSystem.Application;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Exams;
using ContractExamStatus = AIpoweredVivaExamSystem.Application.Contracts.ExamStatus;
using AIpoweredVivaExamSystem.Application.Grading;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure;
using AIpoweredVivaExamSystem.Persistence;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var timer=Stopwatch.StartNew();
var rootIndex=Array.IndexOf(args,"--root");
if(rootIndex<0 || rootIndex+1>=args.Length) throw new ArgumentException("Use tools/demo.ps1 seed from the repository root.");
var root=Path.GetFullPath(args[rootIndex+1]);
var tool=Path.Combine(root,"tools","DemoSeed");
var config=new ConfigurationBuilder().AddEnvironmentVariables().Build();
var recordingRoot=Path.GetFullPath(Path.Combine(tool,".local","recordings"));
if(string.IsNullOrWhiteSpace(config["VivaAudio:RootDirectory"]) ||
    !Path.GetFullPath(config["VivaAudio:RootDirectory"]!).Equals(recordingRoot,StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Demo recordings must use tools/DemoSeed/.local/recordings. Run tools/demo.ps1.");
foreach(var name in new[]{"Ai:ApiKey","ElevenLabs:ApiKey"})
    if(string.IsNullOrWhiteSpace(config[name])) throw new InvalidOperationException($"Missing {name}. Set it in .env or the environment before seeding.");
var connection=config.GetConnectionString("DefaultConnection")??throw new InvalidOperationException("Missing demo connection.");
var sql=new SqlConnectionStringBuilder(connection);
if(sql.InitialCatalog!="AivesPanelDemo" || !sql.IntegratedSecurity ||
    !new[]{"localhost,1433","127.0.0.1,1433"}.Contains(sql.DataSource,StringComparer.OrdinalIgnoreCase))
    throw new InvalidOperationException("Demo reset is restricted to AivesPanelDemo on local SQL Server with Windows authentication.");

var assets=Path.Combine(tool,"assets"); Directory.CreateDirectory(assets);
var lecturerId=Stable("lecturer");var actor=new Actor(lecturerId);
var services=new ServiceCollection().AddLogging(x=>x.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddSingleton<IConfiguration>(config); services.AddSingleton<ICurrentUser>(actor);
services.AddApplication().AddInfrastructure().AddPersistence(config);
services.AddSingleton<IExamNotifier,Notifier>();
using var provider=services.BuildServiceProvider();
// Recordings are fictional student answers, synthesised once; successful assets survive resets.
var speech=provider.GetRequiredService<IExaminerSpeechSynthesizer>();
var recordings=new Dictionary<string,byte[]>();
var scripts=new[]{DemoContent.StrongDi,DemoContent.StrongEf,DemoContent.PartialDi,DemoContent.PartialFollow,DemoContent.PartialEf,DemoContent.TimeoutMvc};
await Parallel.ForEachAsync(scripts,new ParallelOptions{MaxDegreeOfParallelism=2},async(text,ct)=>{
    var key=Hash($"{config["ElevenLabs:VoiceId"]??"iSFxP4Z6YNcx9OXl62Ic"}\n{config["ElevenLabs:Model"]??"eleven_v4_turbo"}\n{text}");
    var path=Path.Combine(assets,$"answer-{key}.mp3");
    byte[] bytes;
    if(File.Exists(path)) bytes=await File.ReadAllBytesAsync(path,ct);
    else {
        bytes=await speech.SynthesizeAsync(text,ct)??throw new InvalidOperationException("ElevenLabs could not create a demo recording. Successful files are kept; rerun to resume.");
        await File.WriteAllBytesAsync(path,bytes,ct);
    }
    lock(recordings) recordings.Add(text,bytes);
});
Console.WriteLine($"Recordings ready: {recordings.Count}, {recordings.Values.Sum(x=>x.Length):N0} bytes (reused when present).");

await ResetDemoDatabaseAsync(sql);
// The resolved absolute target is exactly this tool's generated-recordings directory.
// Retain paid source MP3 assets; only copies for the reset database are disposable.
if(Directory.Exists(recordingRoot)) Directory.Delete(recordingRoot,recursive:true);
using var scope=provider.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
await db.Database.MigrateAsync();
var now=DateTimeOffset.UtcNow;
User Account(string key,string email,string name,UserRole role=UserRole.STUDENT)
{
    var user=new User(email,"",name,UserStatus.Active);db.Entry(user).Property(x=>x.Id).CurrentValue=Stable(key);
    user.AssignRole(role);user.ChangePassword(new PasswordHasher<User>().HashPassword(user,"DemoPass#2026"));db.Users.Add(user);return user;
}
var lecturer=Account("lecturer","giangvien@demo.aives.test","Nguyễn Minh Khôi",UserRole.LECTURER);
Account("admin","quantri@demo.aives.test","Trần Hoàng Phúc",UserRole.ADMIN);
var strong=Account("strong","gioi@demo.aives.test","Nguyễn Hoàng Anh");
var partial=Account("partial","motphan@demo.aives.test","Trần Thảo Vy");
var timeout=Account("timeout","hetgio@demo.aives.test","Lê Minh Đức");
var absent=Account("absent","chuathi@demo.aives.test","Phạm Ngọc Hân");
var live=Account("live","tructiep@demo.aives.test","Võ Gia Bảo");
var subject=new Subject("PRN222","Lập trình ứng dụng .NET với ASP.NET Core và EF Core");
db.AddRange(subject,new LecturerSubject(lecturer.Id,subject.Id));
var topics=DemoContent.Questions.Select(x=>x.Topic).Distinct().ToDictionary(x=>x,x=>new Topic(subject.Id,x));
db.Topics.AddRange(topics.Values);
var questions=new Dictionary<string,(Question Question,Rubric Rubric)>();
foreach(var item in DemoContent.Questions)
{
    var q=new Question(subject.Id,item.Text,BloomLevel.Understand,QuestionDifficulty.Medium,item.Expected,topics[item.Topic]) {CreatedBy=lecturer.Id};
    q.Approve();
    var rubric=new Rubric(q.Id,$"PRN222 · {item.Key}",null,item.Criteria.Select((c,i)=>new CriterionDefinition(null,c.Name,null,c.Concepts,c.Max,i+1)).ToArray());
    foreach(var c in rubric.Criteria) db.Entry(c).Property(x=>x.Id).CurrentValue=Stable($"criterion:{item.Key}:{c.DisplayOrder}");
    db.AddRange(q,rubric);questions[item.Key]=(q,rubric);
}
await db.SaveChangesAsync();
var examService=scope.ServiceProvider.GetRequiredService<IExamService>();
var exam=await examService.CreateAsync(new(subject.Id,"PRN222 · Vấn đáp và chấm điểm hỗ trợ bởi AI",now.AddMinutes(-125),now.AddHours(2),30,2,1),default);
foreach(var q in questions.Values) db.ExamQuestionPools.Add(new ExamQuestionPool(exam.Summary.Id,q.Question.Id));
var roster=new Dictionary<string,ExamStudent>();
foreach(var (key,user,offset) in new[]{("strong",strong,-120),("partial",partial,-90),("timeout",timeout,-60),("absent",absent,-30),("live",live,0)})
{
    var entry=new ExamStudent(exam.Summary.Id,user.Id);entry.Schedule(now.AddMinutes(offset));db.Add(entry);roster[key]=entry;
}
await db.SaveChangesAsync();
// The real lifecycle service checks the approved pool and nonoverlapping in-window roster.
await examService.TransitionAsync(exam.Summary.Id,ContractExamStatus.Published,exam.RowVersion,default);
exam=await examService.GetAsync(exam.Summary.Id,default);
await examService.TransitionAsync(exam.Summary.Id,ContractExamStatus.Ongoing,exam.RowVersion,default);

var storage=scope.ServiceProvider.GetRequiredService<IAnswerAudioStorage>();
async Task<(VivaSession Session,SessionQuestion[] Questions)> SessionAsync(string key,string[] questionKeys,string?[][] answers)
{
    var enrollment=roster[key];var start=enrollment.ScheduledAt!.Value.AddSeconds(5);
    var session=new VivaSession(enrollment.Id,start);db.Add(session);var selected=new List<SessionQuestion>();
    for(var i=0;i<questionKeys.Length;i++)
    {
        var (q,r)=questions[questionKeys[i]];
        var snapshot=JsonSerializer.Serialize(new{r.Name,r.Description,r.TotalScore,Criteria=r.Criteria.OrderBy(c=>c.DisplayOrder),q.ExpectedAnswer});
        var sq=new SessionQuestion(session.Id,q.Id,i+1,q.Content,snapshot);db.Add(sq);selected.Add(sq);
        StudentAnswer? previous=null;
        for(var level=0;level<answers[i].Length;level++)
        {
            var at=start.AddMinutes(i*6+level*2);var text=answers[i][level];
            var turn=new StudentAnswer(sq.Id,level==0?AnswerType.Main:AnswerType.FollowUp,level,level==0?q.Content:DemoContent.FollowDi,at,previous?.Id);
            var answerSeconds=text is null?300:(key,i,level) switch {
                ("strong",0,_)=>96,("strong",1,_)=>130,("partial",0,0)=>38,
                ("partial",0,1)=>82,("partial",1,_)=>64,_=>70
            };
            var answered=at.AddSeconds(answerSeconds);
            turn.SubmitResponse(text??VivaTurnDeadline.NoAnswerMarker,answered);db.Add(turn);
            var next=level+1<answers[i].Length?DemoContent.FollowDi:null;
            db.Add(new AnswerAnalysis(turn.Id,text is not null&&next is null,null,
                text is null?"Hết giờ, không ghi nhận câu trả lời.":"Tình huống minh họa được dựng từ kịch bản demo.",next,text is null?VivaTurnDeadline.TimeoutModelName:"demo-scripted-example",answered));
            if(text is not null)
            {
                var bytes=recordings[text];var path=await storage.SaveAudioAsync(session.Id,turn.Id,bytes,"audio/mpeg",default);
                db.Add(new AnswerAudio(turn.Id,path,"audio/mpeg",bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)),Math.Max(1,(int)Math.Round(bytes.Length/16000d)),answered));
            }
            previous=turn;
        }
    }
    session.Complete(start.AddMinutes(12));await db.SaveChangesAsync();return(session,selected.ToArray());
}
var strongSession=await SessionAsync("strong",["di","ef"],[[DemoContent.StrongDi],[DemoContent.StrongEf]]);
var partialSession=await SessionAsync("partial",["di","ef"],[[DemoContent.PartialDi,DemoContent.PartialFollow],[DemoContent.PartialEf]]);
var timeoutSession=await SessionAsync("timeout",["mvc","ef"],[[DemoContent.TimeoutMvc],[null]]);

var realGrader=scope.ServiceProvider.GetRequiredService<IAiRubricGrader>();
var cachedGrader=new CachedGrader(realGrader,assets);
var gradeStore=scope.ServiceProvider.GetRequiredService<IGradingStore>();
var grading=new GradingService(gradeStore,scope.ServiceProvider.GetRequiredService<ExamAccessGuard>(),actor,cachedGrader);
foreach(var item in new[]{strongSession,timeoutSession})
    foreach(var q in item.Questions)
    {
        await grading.SuggestAsync(item.Session.Id,q.Id,false,default);
        var review=await grading.ReviewAsync(item.Session.Id,default);
        if(review.Questions.Single(x=>x.SessionQuestionId==q.Id).SuggestionState!="ready")
            throw new InvalidOperationException("AI suggestion failed. Assets already generated are kept; rerun to resume.");
        if(cachedGrader.LastGeneratedAt is {} generated && review.Questions.Single(x=>x.SessionQuestionId==q.Id).AiModel!="server-no-answer")
            await db.QuestionGradings.IgnoreQueryFilters().Where(g=>g.SessionQuestionId==q.Id).ExecuteUpdateAsync(s=>s.SetProperty(g=>g.SuggestedAt,(DateTimeOffset?)generated));
    }
var finalReview=await grading.ReviewAsync(timeoutSession.Session.Id,default);
await grading.SaveAsync(timeoutSession.Session.Id,new GradeInput{Version=finalReview.Version,OverallComment="Em nắm được các thành phần MVC nhưng cần ôn lại luồng request và EF Core. Câu chưa trả lời được chấm 0; nên luyện trình bày ngắn gọn theo từng ý.",
    Questions=finalReview.Questions.Select(q=>new QuestionGradeInput{SessionQuestionId=q.SessionQuestionId,
        Comment=q.Position==1?"Đã nêu đúng vai trò cơ bản; cần bổ sung routing, model binding và validation.":"Hết thời gian, không ghi nhận nội dung để cho điểm.",
        Criteria=q.Criteria.Select((c,i)=>new CriterionGradeInput{CriterionId=c.Id,Score=q.Position==1?Math.Min(c.MaxScore,3-i):0}).ToList()}).ToList()},true,default);
var list=await grading.ListAsync(exam.Summary.Id,default);
if(list.Sessions.Count!=5 || list.Sessions.Count(s=>s.State=="not-started")!=2 ||
    list.Sessions.Single(s=>s.SessionId==strongSession.Session.Id).State!="awaiting-review" ||
    list.Sessions.Single(s=>s.SessionId==timeoutSession.Session.Id).State!="finalised" ||
    (await grading.ReviewAsync(partialSession.Session.Id,default)).Questions.Any(q=>q.SuggestionState!="empty"))
    throw new InvalidOperationException("Demo state verification failed.");
var local=Path.Combine(tool,".local");Directory.CreateDirectory(local);
await File.WriteAllTextAsync(Path.Combine(local,"manifest.json"),JsonSerializer.Serialize(new{ExamId=exam.Summary.Id,LiveEnrollmentId=roster["live"].Id,
    StrongSessionId=strongSession.Session.Id,PartialSessionId=partialSession.Session.Id,FinalisedSessionId=timeoutSession.Session.Id},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Ready in {timer.Elapsed.TotalSeconds:F1}s · AivesPanelDemo · 6 approved questions / 3 topics / 5 students.");
Console.WriteLine($"Live slot: {now.ToOffset(TimeSpan.FromHours(7)):HH:mm}–{now.AddMinutes(30).ToOffset(TimeSpan.FromHours(7)):HH:mm} (Vietnam), 300 seconds per turn.");
Console.WriteLine($"Grading: http://127.0.0.1:5192/Grading/{exam.Summary.Id}");
Console.WriteLine("All demo accounts use DemoPass#2026. Start: ./tools/demo.ps1 start. Presenter steps: docs/GROUP_1_SETUP.md.");

static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
static Guid Stable(string key)=>new(SHA256.HashData(Encoding.UTF8.GetBytes($"aives-panel-demo:{key}"))[..16]);
static async Task ResetDemoDatabaseAsync(SqlConnectionStringBuilder sql)
{
    const string marker="tools/DemoSeed/v1";
    var master=new SqlConnectionStringBuilder(sql.ConnectionString){InitialCatalog="master"};
    await using var connection=new SqlConnection(master.ConnectionString);await connection.OpenAsync();
    await using var exists=new SqlCommand("SELECT DB_ID(N'AivesPanelDemo')",connection);
    if(await exists.ExecuteScalarAsync() is not DBNull)
    {
        await using var target=new SqlConnection(sql.ConnectionString);await target.OpenAsync();
        await using var check=new SqlCommand("SELECT CONVERT(nvarchar(100),value) FROM sys.extended_properties WHERE class=0 AND name=N'AivesDemoSeed'",target);
        if((string?)await check.ExecuteScalarAsync()!=marker) throw new InvalidOperationException("AivesPanelDemo already exists without this tool's ownership marker; refusing to reset it.");
        await target.CloseAsync();SqlConnection.ClearAllPools();
        await using var drop=new SqlCommand("ALTER DATABASE [AivesPanelDemo] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [AivesPanelDemo];",connection);
        await drop.ExecuteNonQueryAsync();
    }
    await using(var create=new SqlCommand("CREATE DATABASE [AivesPanelDemo]",connection)) await create.ExecuteNonQueryAsync();
    await using var created=new SqlConnection(sql.ConnectionString);await created.OpenAsync();
    await using var mark=new SqlCommand("EXEC sys.sp_addextendedproperty @name=N'AivesDemoSeed', @value=N'tools/DemoSeed/v1'",created);await mark.ExecuteNonQueryAsync();
    Console.WriteLine("Reset dedicated local AivesPanelDemo; applying committed migrations.");
}
sealed record Actor(Guid Id):ICurrentUser {public bool IsAdministrator=>false;public bool IsLecturer=>true;}
sealed class Notifier:IExamNotifier {public Task NotifyChangedAsync(Guid examId,CancellationToken ct)=>Task.CompletedTask;}
sealed record SavedSuggestion(DateTimeOffset GeneratedAt,QuestionSuggestion Suggestion);
sealed class CachedGrader(IAiRubricGrader real,string assets):IAiRubricGrader
{
    public DateTimeOffset? LastGeneratedAt {get;private set;}
    public async Task<QuestionSuggestion> SuggestAsync(QuestionGradingContext context,CancellationToken ct)
    {
        var hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context))).ToLowerInvariant();
        var path=Path.Combine(assets,$"suggestion-{hash}.json");SavedSuggestion saved;
        if(File.Exists(path)) saved=JsonSerializer.Deserialize<SavedSuggestion>(await File.ReadAllTextAsync(path,ct))!;
        else {
            var watch=Stopwatch.StartNew();var suggestion=await real.SuggestAsync(context,ct);saved=new(DateTimeOffset.UtcNow,suggestion);
            await File.WriteAllTextAsync(path,JsonSerializer.Serialize(saved,new JsonSerializerOptions{WriteIndented=true}),ct);
            Console.WriteLine($"Generated real AI suggestion in {watch.Elapsed.TotalSeconds:F1}s, total {saved.Suggestion.Criteria.Sum(c=>c.Score)}/10.");
        }
        LastGeneratedAt=saved.GeneratedAt;return GradingRules.ValidateSuggestion(context,saved.Suggestion);
    }
}
