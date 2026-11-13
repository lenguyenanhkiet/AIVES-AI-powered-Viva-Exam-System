using System.Text.Json;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.Grading;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class GradingStore(ApplicationDbContext db,IExamUnitOfWork uow,TimeProvider? clock=null) : IGradingStore
{
    public async Task<Guid> ExamForSessionAsync(Guid sessionId,CancellationToken ct) =>
        await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
               join r in db.ExamStudents.IgnoreQueryFilters() on s.ExamStudentId equals r.Id
               where s.Id==sessionId select (Guid?)r.ExamId).SingleOrDefaultAsync(ct)
        ?? throw new ResourceNotFoundException("Không tìm thấy phiên thi.");

    private async Task CloseExpiredAsync(Guid examId,CancellationToken ct)
    {
        await uow.ExecuteAsync(async token => {
            var exam=await ExamLock.LockAsync(db,examId,token);
            var entries=await (from s in db.VivaSessions.IgnoreQueryFilters()
                join r in db.ExamStudents.IgnoreQueryFilters() on s.ExamStudentId equals r.Id
                where r.ExamId==examId && s.CompletedAt==null select new {Session=s,r.ScheduledAt}).ToArrayAsync(token);
            var now=(clock??TimeProvider.System).GetUtcNow();
            foreach(var entry in entries)
            {
                var end=exam.SlotEnd(entry.ScheduledAt??entry.Session.StartedAt);
                if(end>exam.EndAt) end=exam.EndAt;
                if(now<=end.AddSeconds(VivaTurnDeadline.GraceSeconds)) continue;
                if(!await VivaSubmissionLock.TryProtectExpiryAsync(db,entry.Session.Id,token)) continue;
                var turns=await db.StudentAnswers.IgnoreQueryFilters().Where(a=>a.AnsweredAt==null &&
                    db.SessionQuestions.IgnoreQueryFilters().Any(q=>q.Id==a.SessionQuestionId && q.VivaSessionId==entry.Session.Id)).ToArrayAsync(token);
                foreach(var turn in turns)
                {
                    var at=end<turn.AskedAt?turn.AskedAt:end;
                    turn.SubmitResponse(VivaTurnDeadline.NoAnswerMarker,at);
                    db.AnswerAnalyses.Add(new AnswerAnalysis(turn.Id,false,null,"Slot ended without a submitted answer.",null,VivaTurnDeadline.TimeoutModelName,at));
                }
                entry.Session.Complete(end<entry.Session.StartedAt?entry.Session.StartedAt:end);
            }
        },[],ct);
    }

    private async Task EnsureAsync(Guid sessionId,CancellationToken ct)
    {
        var examId=await ExamForSessionAsync(sessionId,ct);
        await CloseExpiredAsync(examId,ct);
        if(await db.SessionGradings.IgnoreQueryFilters().AnyAsync(g=>g.VivaSessionId==sessionId,ct)) return;
        await uow.ExecuteAsync(async token=>{
            await ExamLock.LockAsync(db,examId,token);
            if(await db.SessionGradings.IgnoreQueryFilters().AnyAsync(g=>g.VivaSessionId==sessionId,token)) return;
            db.SessionGradings.Add(new SessionGrading(sessionId));
            var ids=await db.SessionQuestions.IgnoreQueryFilters().Where(q=>q.VivaSessionId==sessionId).Select(q=>q.Id).ToArrayAsync(token);
            foreach(var id in ids) db.QuestionGradings.Add(new QuestionGrading(id));
        },[],ct);
    }

    public async Task<GradingExamList> ListAsync(Guid examId,CancellationToken ct)
    {
        await CloseExpiredAsync(examId,ct);
        var exam=await db.Exams.IgnoreQueryFilters().AsNoTracking().SingleAsync(e=>e.Id==examId,ct);
        var rows=await (from r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking()
            join user in db.Users.AsNoTracking() on r.StudentId equals user.Id
            join session in db.VivaSessions.IgnoreQueryFilters().AsNoTracking() on r.Id equals session.ExamStudentId into sessions
            from session in sessions.DefaultIfEmpty()
            where r.ExamId==examId select new {r.Id,user.FullName,user.Email,Session=session}).OrderBy(x=>x.FullName).ThenBy(x=>x.Id).ToArrayAsync(ct);
        var result=new List<GradingSessionRow>();
        foreach(var row in rows)
        {
            if(row.Session is null) { result.Add(new(row.Id,null,row.FullName,row.Email,null,null,"not-started",null,null,null,0)); continue; }
            var questions=await QuestionsAsync(row.Session.Id,exam,ct);
            var grade=await db.SessionGradings.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(g=>g.VivaSessionId==row.Session.Id,ct);
            var lecturerTotal=TotalEntered(questions);
            result.Add(new(row.Id,row.Session.Id,row.FullName,row.Email,row.Session.StartedAt,row.Session.CompletedAt,State(row.Session,grade),
                grade?.IsFinalised==true?lecturerTotal:null,TotalSuggested(questions),lecturerTotal,questions.Sum(q=>q.MaxScore)));
        }
        return new(examId,exam.Name,result);
    }

    public async Task<SessionReview> ReviewAsync(Guid sessionId,CancellationToken ct)
    {
        await EnsureAsync(sessionId,ct);
        var info=await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
            join r in db.ExamStudents.IgnoreQueryFilters() on s.ExamStudentId equals r.Id
            join e in db.Exams.IgnoreQueryFilters() on r.ExamId equals e.Id
            join user in db.Users on r.StudentId equals user.Id
            where s.Id==sessionId select new {Session=s,Exam=e,user.FullName,user.Email}).SingleAsync(ct);
        var grade=await db.SessionGradings.IgnoreQueryFilters().AsNoTracking().SingleAsync(g=>g.VivaSessionId==sessionId,ct);
        var questions=await QuestionsAsync(sessionId,info.Exam,ct);
        var neighbours=await (from r in db.ExamStudents.IgnoreQueryFilters()
            join user in db.Users on r.StudentId equals user.Id
            join s in db.VivaSessions.IgnoreQueryFilters() on r.Id equals s.ExamStudentId
            where r.ExamId==info.Exam.Id orderby user.FullName,r.Id select s.Id).ToArrayAsync(ct);
        var index=Array.IndexOf(neighbours,sessionId);
        var by=grade.FinalisedBy.HasValue?await db.Users.Where(u=>u.Id==grade.FinalisedBy).Select(u=>u.FullName).SingleOrDefaultAsync(ct):null;
        return new(sessionId,info.Exam.Id,info.Exam.Name,info.FullName,info.Email,info.Session.StartedAt,info.Session.CompletedAt,State(info.Session,grade),
            Convert.ToBase64String(grade.RowVersion),info.Session.CompletedAt.HasValue&&!grade.IsFinalised,grade.IsFinalised,
            index>0?neighbours[index-1]:null,index>=0&&index+1<neighbours.Length?neighbours[index+1]:null,
            questions,grade.OverallComment,TotalSuggested(questions),TotalEntered(questions),questions.Sum(q=>q.MaxScore),
            grade.IsFinalised?grade.FinalisedAt:null,grade.IsFinalised?by:null,
            new(questions.Sum(q=>q.Signals.AnswerSeconds),questions.FirstOrDefault()?.Signals.TurnLimitSeconds??0,
                questions.Sum(q=>q.Signals.FollowUpCount),questions.Sum(q=>q.Signals.AudioSeconds),questions.Sum(q=>q.Signals.AnswerLength)));
    }

    private static string State(VivaSession session,SessionGrading? grade) =>
        !session.CompletedAt.HasValue?"in-progress":grade?.IsFinalised==true?"finalised":grade?.EditedAt.HasValue==true?"draft":"awaiting-review";
    private static decimal? TotalSuggested(IReadOnlyList<QuestionReview> questions) =>
        questions.Count>0&&questions.All(q=>q.SuggestedTotal.HasValue)?questions.Sum(q=>q.SuggestedTotal!.Value):null;
    private static decimal? TotalEntered(IReadOnlyList<QuestionReview> questions) =>
        questions.SelectMany(q=>q.Criteria).Any(c=>c.LecturerScore.HasValue)?questions.Sum(q=>q.Criteria.Sum(c=>c.LecturerScore??0)):null;

    private async Task<IReadOnlyList<QuestionReview>> QuestionsAsync(Guid sessionId,Exam exam,CancellationToken ct)
    {
        var questions=await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking().Where(q=>q.VivaSessionId==sessionId).OrderBy(q=>q.Position).ToArrayAsync(ct);
        var ids=questions.Select(q=>q.Id).ToArray();
        var turns=await db.StudentAnswers.IgnoreQueryFilters().AsNoTracking().Where(t=>ids.Contains(t.SessionQuestionId)).OrderBy(t=>t.FollowUpLevel).ToArrayAsync(ct);
        var turnIds=turns.Select(t=>t.Id).ToArray();
        var audios=await db.AnswerAudios.IgnoreQueryFilters().AsNoTracking().Where(a=>turnIds.Contains(a.StudentAnswerId)).ToArrayAsync(ct);
        var timeouts=await db.AnswerAnalyses.IgnoreQueryFilters().AsNoTracking().Where(a=>turnIds.Contains(a.StudentAnswerId)&&a.ModelName==VivaTurnDeadline.TimeoutModelName).Select(a=>a.StudentAnswerId).ToArrayAsync(ct);
        var grades=await db.QuestionGradings.IgnoreQueryFilters().AsNoTracking().Where(g=>ids.Contains(g.SessionQuestionId)).ToArrayAsync(ct);
        var limit=VivaInterviewService.CalculateTurnLimitSeconds(exam.DurationMinutes,exam.NumberOfMainQuestions,exam.MaxFollowUpPerQuestion);
        return questions.Select(q=>{
            var rubric=GradingRules.ReadRubric(q.RubricSnapshot);
            var grade=grades.SingleOrDefault(g=>g.SessionQuestionId==q.Id);
            var suggestion=grade?.AiSuggestionJson is {} json?JsonSerializer.Deserialize<QuestionSuggestion>(json):null;
            if(suggestion?.ModelName=="server-no-answer") suggestion=GradingRules.NoAnswerSuggestion(rubric.Criteria);
            var entered=grade?.LecturerScoresJson is {} scores?JsonSerializer.Deserialize<CriterionGradeInput[]>(scores)!:[];
            var criteria=rubric.Criteria.Select(c=>new CriterionReview(c.Id,c.Name,c.Description,c.ExpectedConcepts,c.MaxScore,c.DisplayOrder,
                suggestion?.Criteria.Single(s=>s.CriterionId==c.Id).Score,suggestion?.Criteria.Single(s=>s.CriterionId==c.Id).Justification,
                entered.SingleOrDefault(s=>s.CriterionId==c.Id)?.Score)).ToArray();
            var evidence=turns.Where(t=>t.SessionQuestionId==q.Id).Select(t=>{
                var audio=audios.SingleOrDefault(a=>a.StudentAnswerId==t.Id);
                var transcript=GradingRules.IsNoAnswerMarker(t.Transcript)?null:t.Transcript;
                return new TurnEvidence(t.Id,(AIpoweredVivaExamSystem.Application.Contracts.AnswerType)t.AnswerType,t.FollowUpLevel,t.PromptText,transcript,t.AskedAt,t.AnsweredAt,timeouts.Contains(t.Id),
                    Math.Max(0,((t.AnsweredAt??DateTimeOffset.UtcNow)-t.AskedAt).TotalSeconds),limit,audio?.DurationSeconds,
                    transcript?.Length??0,audio is null?null:$"/Grading/Audio/{audio.Id}",audio?.MimeType);
            }).ToArray();
            var pending=grade?.SuggestionAttemptId.HasValue==true && grade.SuggestionStartedAt>DateTimeOffset.UtcNow.AddMinutes(-2);
            var state=pending?"pending":grade?.SuggestionError is not null?"failed":suggestion is not null?"ready":"empty";
            return new QuestionReview(q.Id,q.Position,q.ContentSnapshot,rubric.ExpectedAnswer,criteria,evidence,
                new(evidence.Sum(t=>t.AnswerSeconds),limit,evidence.Count(t=>t.AnswerType==AIpoweredVivaExamSystem.Application.Contracts.AnswerType.FollowUp),evidence.Sum(t=>t.AudioDurationSeconds??0),evidence.Sum(t=>t.AnswerLength)),
                state,grade?.SuggestionError is null?null:GradingRules.UnavailableSuggestionMessage,grade?.AiModel,grade?.SuggestedAt,suggestion?.Strengths,suggestion?.Weaknesses,suggestion?.MissingPoints,
                suggestion is null?null:suggestion.Criteria.Sum(c=>c.Score),criteria.Any(c=>c.LecturerScore.HasValue)?criteria.Sum(c=>c.LecturerScore??0):null,
                criteria.Sum(c=>c.MaxScore),grade?.LecturerComment);
        }).ToArray();
    }

    public async Task SaveAsync(Guid sessionId,GradeInput input,bool finalise,Guid lecturerId,CancellationToken ct)
    {
        var review=await ReviewAsync(sessionId,ct);
        var errors=new Dictionary<string,string>();
        if(!review.CanEdit) throw new ResourceConflictException("Chỉ chấm khi phiên thi đã kết thúc. Mở lại bài đã chốt trước khi sửa điểm.");
        if(input.OverallComment?.Length>4000) errors["Input.OverallComment"]="Nhập tối đa 4000 ký tự.";
        if(input.Questions.Count!=review.Questions.Count || input.Questions.Select(q=>q.SessionQuestionId).Distinct().Count()!=review.Questions.Count)
            errors["Input.Questions"]="Gửi đầy đủ các câu hỏi, mỗi câu một lần.";
        for(var i=0;i<input.Questions.Count;i++)
        {
            var posted=input.Questions[i]; var question=review.Questions.SingleOrDefault(q=>q.SessionQuestionId==posted.SessionQuestionId);
            if(question is null) { errors[$"Input.Questions[{i}]"]="Câu hỏi không hợp lệ."; continue; }
            if(posted.Comment?.Length>2000) errors[$"Input.Questions[{i}].Comment"]="Nhập tối đa 2000 ký tự.";
            if(posted.Criteria.Count!=question.Criteria.Count || posted.Criteria.Select(c=>c.CriterionId).Distinct().Count()!=question.Criteria.Count)
                errors[$"Input.Questions[{i}].Criteria"]="Gửi đầy đủ các tiêu chí của rubric, mỗi tiêu chí một lần.";
            for(var j=0;j<posted.Criteria.Count;j++)
            {
                var value=posted.Criteria[j]; var criterion=question.Criteria.SingleOrDefault(c=>c.Id==value.CriterionId);
                var key=$"Input.Questions[{i}].Criteria[{j}].Score";
                if(criterion is null) errors[key]="Tiêu chí rubric không hợp lệ.";
                else if(value.Score is {} score && (score<0 || score>criterion.MaxScore || decimal.Round(score,2)!=score)) errors[key]=$"Nhập điểm từ 0 đến {criterion.MaxScore}, tối đa hai chữ số thập phân.";
                else if(finalise && value.Score is null) errors[key]="Nhập điểm tiêu chí này trước khi chốt.";
            }
        }
        if(errors.Count>0) throw new GradingValidationException(errors);
        await uow.ExecuteAsync(async token=>{
            await ExamLock.LockAsync(db,review.ExamId,token);
            var grade=await db.SessionGradings.IgnoreQueryFilters().SingleAsync(g=>g.VivaSessionId==sessionId,token);
            await db.Entry(grade).ReloadAsync(token); CheckVersion(grade,input.Version);
            if(grade.IsFinalised) throw new ResourceConflictException("Mở lại bài đã chốt trước khi sửa điểm.");
            var ids=review.Questions.Select(q=>q.SessionQuestionId).ToArray();
            var questions=await db.QuestionGradings.IgnoreQueryFilters().Where(g=>ids.Contains(g.SessionQuestionId)).ToArrayAsync(token);
            foreach(var question in questions)
            {
                var value=input.Questions.Single(q=>q.SessionQuestionId==question.SessionQuestionId);
                question.SetLecturerScores(JsonSerializer.Serialize(value.Criteria),value.Comment);
            }
            grade.Save(input.OverallComment,lecturerId,finalise);
        },[],ct);
    }
    private static void CheckVersion(SessionGrading grade,string version)
    {
        byte[] bytes; try { bytes=Convert.FromBase64String(version); } catch(Exception ex) when(ex is FormatException or ArgumentNullException) { throw new ResourceConflictException("Tải lại trang chấm bài trước khi lưu."); }
        if(!bytes.SequenceEqual(grade.RowVersion)) throw new ResourceConflictException("Bài chấm đã được cập nhật ở nơi khác. Tải lại trang trước khi lưu thay đổi.");
    }
    public async Task ReopenAsync(Guid sessionId,string version,Guid lecturerId,CancellationToken ct)
    {
        var examId=await ExamForSessionAsync(sessionId,ct);
        await uow.ExecuteAsync(async token=>{
            await ExamLock.LockAsync(db,examId,token);
            var grade=await db.SessionGradings.IgnoreQueryFilters().SingleAsync(g=>g.VivaSessionId==sessionId,token);
            await db.Entry(grade).ReloadAsync(token); CheckVersion(grade,version); grade.Reopen(lecturerId);
        },[],ct);
    }

    public async Task<(Guid AttemptId,QuestionGradingContext Context)?> BeginSuggestionAsync(Guid sessionId,Guid questionId,bool regenerate,CancellationToken ct)
    {
        var review=await ReviewAsync(sessionId,ct);
        if(!review.CompletedAt.HasValue) throw new ResourceConflictException("Chỉ yêu cầu gợi ý sau khi phiên thi đã kết thúc.");
        var question=review.Questions.SingleOrDefault(q=>q.SessionQuestionId==questionId)??throw new ResourceNotFoundException("Không tìm thấy câu hỏi.");
        if(!regenerate && question.SuggestedAt.HasValue) return null;
        var attemptId=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        var claimed=await db.QuestionGradings.IgnoreQueryFilters().Where(g=>g.SessionQuestionId==questionId &&
            (g.SuggestionAttemptId==null || g.SuggestionStartedAt<now.AddMinutes(-2)))
            .ExecuteUpdateAsync(s=>s.SetProperty(g=>g.SuggestionAttemptId,(Guid?)attemptId).SetProperty(g=>g.SuggestionStartedAt,(DateTimeOffset?)now).SetProperty(g=>g.SuggestionError,(string?)null),ct);
        if(claimed==0) throw new ResourceConflictException("Đang tạo gợi ý. Bạn vẫn có thể tự chấm theo rubric.");
        return (attemptId,new QuestionGradingContext(question.QuestionText,question.ExpectedAnswer,
            question.Criteria.Select(c=>new GradingCriterion(c.Id,c.Name,c.Description,c.ExpectedConcepts,c.MaxScore,c.DisplayOrder)).ToArray(),
            question.Turns.Select(t=>new GradeConversation(t.PromptText,t.Transcript)).ToArray()));
    }
    public async Task FinishSuggestionAsync(Guid questionId,Guid attemptId,QuestionSuggestion? suggestion,string? error,CancellationToken ct)
    {
        var query=db.QuestionGradings.IgnoreQueryFilters().Where(g=>g.SessionQuestionId==questionId && g.SuggestionAttemptId==attemptId);
        if(suggestion is null) await query.ExecuteUpdateAsync(s=>s.SetProperty(g=>g.SuggestionAttemptId,(Guid?)null).SetProperty(g=>g.SuggestionError,error),ct);
        else {
            var json=JsonSerializer.Serialize(suggestion); var at=DateTimeOffset.UtcNow;
            await query.ExecuteUpdateAsync(s=>s.SetProperty(g=>g.AiSuggestionJson,json).SetProperty(g=>g.AiModel,suggestion.ModelName)
                .SetProperty(g=>g.SuggestedAt,(DateTimeOffset?)at).SetProperty(g=>g.SuggestionAttemptId,(Guid?)null).SetProperty(g=>g.SuggestionError,(string?)null),ct);
        }
    }
    public async Task<GradingAudio> AudioAsync(Guid audioId,CancellationToken ct) =>
        await (from a in db.AnswerAudios.IgnoreQueryFilters().AsNoTracking()
            join t in db.StudentAnswers.IgnoreQueryFilters() on a.StudentAnswerId equals t.Id
            join q in db.SessionQuestions.IgnoreQueryFilters() on t.SessionQuestionId equals q.Id
            join s in db.VivaSessions.IgnoreQueryFilters() on q.VivaSessionId equals s.Id
            join r in db.ExamStudents.IgnoreQueryFilters() on s.ExamStudentId equals r.Id
            where a.Id==audioId select new GradingAudio(r.ExamId,a.FilePath,a.MimeType)).SingleOrDefaultAsync(ct)
        ?? throw new ResourceNotFoundException("Không tìm thấy bản ghi âm.");
}
