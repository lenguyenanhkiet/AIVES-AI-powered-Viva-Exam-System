using AIpoweredVivaExamSystem.DataAccess.Contracts;
using System.Security.Cryptography;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Authentication;
using AIpoweredVivaExamSystem.DataAccess.Models.Common;
using AIpoweredVivaExamSystem.DataAccess.Contracts.Exams;
using AIpoweredVivaExamSystem.DataAccess.Models.Exams;
using AIpoweredVivaExamSystem.DataAccess.Contracts.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Models.VivaSessions;
using AIpoweredVivaExamSystem.DataAccess.Common;
using AIpoweredVivaExamSystem.DataAccess.Entities;
using AIpoweredVivaExamSystem.DataAccess.Enums;
using AIpoweredVivaExamSystem.DataAccess.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.DataAccess.Repositories;

public sealed class VivaInterviewStore(
    ApplicationDbContext db,
    IExamUnitOfWork uow,
    IAnswerAudioStorage audioStorage,
    IDataActor actor) : IVivaInterviewStore
{
    public Task<IAsyncDisposable> AcquireSubmissionAsync(Guid sessionId, CancellationToken ct) =>
        VivaSubmissionLock.AcquireAsync(db, sessionId, ct);

    public async Task<string> GetSubjectHintAsync(Guid sessionId, CancellationToken ct)
    {
        var info = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                          join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                          join e in db.Exams.IgnoreQueryFilters().AsNoTracking() on r.ExamId equals e.Id
                          join sub in db.Subjects.IgnoreQueryFilters().AsNoTracking() on e.SubjectId equals sub.Id
                          where s.Id == sessionId && r.StudentId == actor.Id
                          select new { sub.Code, sub.Name, ExamName = e.Name }).SingleOrDefaultAsync(ct)
                   ?? throw new ResourceNotFoundException("Session not found.");

        return $"{info.Code} - {info.Name} ({info.ExamName})";
    }

    public async Task<VivaInterviewState> GetStateAsync(Guid sessionId, CancellationToken ct)
    {
        var meta = await LoadSessionMetadataAsync(sessionId, ct);
        if (!meta.Session.CompletedAt.HasValue)
        {
            var hasAnyTurn = await db.StudentAnswers.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(a => db.SessionQuestions.IgnoreQueryFilters().Any(sq => sq.Id == a.SessionQuestionId && sq.VivaSessionId == sessionId), ct);

            if (!hasAnyTurn)
            {
                await EnsureInitialTurnAsync(meta.ExamId, meta.EnrollmentId, sessionId, ct);
            }
        }

        return await ProjectStateAsync(sessionId, ct);
    }

    public async Task<ExaminerEvaluationContext> ReadEvaluationContextAsync(SubmitAnswerRequest request, CancellationToken ct)
    {
        var meta = await LoadSessionMetadataAsync(request.SessionId, ct);
        var exam = await db.Exams.IgnoreQueryFilters().AsNoTracking().SingleAsync(e => e.Id == meta.ExamId, ct);
        var question = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(q => q.VivaSessionId == request.SessionId && db.StudentAnswers.IgnoreQueryFilters()
                .Any(t => t.Id == request.TurnId && t.SessionQuestionId == q.Id), ct);
        var turns = await db.StudentAnswers.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.SessionQuestionId == question.Id).OrderBy(t => t.FollowUpLevel).ToArrayAsync(ct);
        var current = turns.Single(t => t.Id == request.TurnId);
        var subjectName = await db.Subjects.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == exam.SubjectId).Select(x => x.Name).SingleAsync(ct);
        var history = turns.Where(t => t.AnsweredAt.HasValue)
            .Select(t => new ConversationTurn(t.AnswerType, t.FollowUpLevel, t.PromptText, t.Transcript ?? ""))
            .Append(new ConversationTurn(current.AnswerType, current.FollowUpLevel, current.PromptText, request.Transcript ?? "")).ToArray();
        return new(subjectName, question.ContentSnapshot, question.RubricSnapshot,
            current.FollowUpLevel, exam.MaxFollowUpPerQuestion, history);
    }

    public async Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, DateTimeOffset receivedAt, ExaminerDecision evaluatedDecision, CancellationToken ct)
    {
        var meta = await LoadSessionMetadataAsync(request.SessionId, ct);
        var transcript = DomainRules.RequiredText(request.Transcript, nameof(request.Transcript), 8000);

        byte[]? validAudio = null;
        string? validMime = null;
        string? audioHash = null;
        string? savedAudioPath = null;

        if (request.AudioBytes is { Length: > 0 } bytes)
        {
            validMime = AnswerRecording.Validate(bytes, request.AudioMimeType);
            validAudio = bytes;
            audioHash = Convert.ToHexString(SHA256.HashData(bytes));
        }

        var changed = new List<Guid>();
        await uow.ExecuteAsync(async token =>
        {
            var exam = await ExamLock.LockAsync(db, meta.ExamId, token);
            var enrollment = await LockEnrollmentAsync(meta.EnrollmentId, token);
            var session = await db.VivaSessions.IgnoreQueryFilters().SingleAsync(s => s.Id == request.SessionId, token);

            var now = receivedAt;
            var slotStart = enrollment.ScheduledAt ?? DateTimeOffset.MaxValue;
            var slotEnd = slotStart == DateTimeOffset.MaxValue ? slotStart : exam.SlotEnd(slotStart);
            if (slotEnd > exam.EndAt) slotEnd = exam.EndAt;
            if (exam.Status != ExamStatus.Ongoing || now < slotStart)
            {
                throw new DomainValidationException("Thời gian ca thi đã kết thúc hoặc kỳ thi không còn mở.");
            }
            var slotTimedOut = now >= slotEnd || VivaTurnDeadline.HasExpired(slotEnd, now, request.TimedOut);

            var questions = await db.SessionQuestions.IgnoreQueryFilters()
                .Where(q => q.VivaSessionId == session.Id)
                .OrderBy(q => q.Position)
                .ToArrayAsync(token);

            var qIds = questions.Select(q => q.Id).ToArray();
            var turns = await db.StudentAnswers.IgnoreQueryFilters()
                .Where(a => qIds.Contains(a.SessionQuestionId))
                .OrderBy(a => a.AskedAt).ThenBy(a => a.FollowUpLevel)
                .ToListAsync(token);

            var currentTurn = turns.SingleOrDefault(t => t.Id == request.TurnId)
                ?? throw new ResourceNotFoundException("Không tìm thấy lượt câu hỏi cần trả lời.");
            await db.Entry(currentTurn).ReloadAsync(token);
            await db.Entry(session).ReloadAsync(token);

            // Idempotent handling: if this turn was already answered in a concurrent request, return cleanly.
            if (currentTurn.AnsweredAt.HasValue)
                return;

            if (session.CompletedAt.HasValue)
                throw new DomainValidationException("Phiên thi đã kết thúc, không thể nộp thêm câu trả lời.");

            var activeUnanswered = turns.FirstOrDefault(t => !t.AnsweredAt.HasValue);
            if (activeUnanswered is not null && activeUnanswered.Id != currentTurn.Id)
                throw new ResourceConflictException("Lượt câu hỏi hiện tại đã thay đổi. Hãy tải lại phiên thi.");

            var limit = VivaTiming.CalculateTurnLimitSeconds(
                exam.DurationMinutes, exam.NumberOfMainQuestions, exam.MaxFollowUpPerQuestion);
            var timedOut = slotTimedOut || VivaTurnDeadline.HasExpired(
                currentTurn.AskedAt.AddSeconds(limit), now, request.TimedOut);
            var tooLate = now > slotEnd.AddSeconds(VivaTurnDeadline.GraceSeconds) ||
                now > currentTurn.AskedAt.AddSeconds(limit + VivaTurnDeadline.GraceSeconds);
            if (tooLate)
            {
                transcript = VivaTurnDeadline.NoAnswerMarker;
                validAudio = null;
            }
            if (validAudio is not null)
                savedAudioPath = await audioStorage.SaveAudioAsync(request.SessionId, request.TurnId, validAudio, validMime!, token);
            var answerTimestamp = now < currentTurn.AskedAt ? currentTurn.AskedAt : now;
            currentTurn.SubmitResponse(transcript, answerTimestamp);

            if (validAudio is not null && savedAudioPath is not null && validMime is not null && audioHash is not null)
            {
                var existingAudio = await db.AnswerAudios.IgnoreQueryFilters()
                    .AnyAsync(a => a.StudentAnswerId == currentTurn.Id, token);
                if (!existingAudio)
                {
                    db.AnswerAudios.Add(new AnswerAudio(
                        currentTurn.Id,
                        savedAudioPath,
                        validMime,
                        validAudio.LongLength,
                        audioHash,
                        request.AudioDurationSeconds,
                        answerTimestamp));
                }
            }

            var currentQuestion = questions.Single(q => q.Id == currentTurn.SessionQuestionId);
            var remainingSlotSeconds = (slotEnd - answerTimestamp).TotalSeconds;
            var canAskFollowUp = !timedOut && currentTurn.FollowUpLevel < exam.MaxFollowUpPerQuestion && remainingSlotSeconds >= 15;

            var decision = timedOut
                ? new ExaminerDecision(false, null, "Turn deadline expired; advanced without AI evaluation.", null, VivaTurnDeadline.TimeoutModelName)
                : evaluatedDecision;
            var followUpPrompt = canAskFollowUp && !decision.IsComplete && !string.IsNullOrWhiteSpace(decision.FollowUpPrompt)
                ? decision.FollowUpPrompt.Trim()
                : null;

            db.AnswerAnalyses.Add(new AnswerAnalysis(
                currentTurn.Id,
                decision.IsComplete,
                decision.MissingConcepts,
                decision.AnalysisSummary,
                followUpPrompt,
                decision.ModelName,
                answerTimestamp));

            var advanceTimestamp = DateTimeOffset.UtcNow;
            if (slotTimedOut || advanceTimestamp >= slotEnd)
            {
                session.Complete(advanceTimestamp);
                changed.Add(meta.ExamId);
                return;
            }

            var advanceToNextMain = true;
            if (followUpPrompt is not null)
            {
                var nextFollowUp = new StudentAnswer(
                    currentQuestion.Id,
                    AnswerType.FollowUp,
                    currentTurn.FollowUpLevel + 1,
                    followUpPrompt,
                    advanceTimestamp,
                    currentTurn.Id);
                db.StudentAnswers.Add(nextFollowUp);
                advanceToNextMain = false;
            }

            if (advanceToNextMain)
            {
                var nextQuestion = questions.FirstOrDefault(q => q.Position > currentQuestion.Position);
                if (nextQuestion is not null)
                {
                    var alreadySeeded = turns.Any(t => t.SessionQuestionId == nextQuestion.Id && t.FollowUpLevel == 0);
                    if (!alreadySeeded)
                    {
                        db.StudentAnswers.Add(new StudentAnswer(
                            nextQuestion.Id,
                            AnswerType.Main,
                            0,
                            nextQuestion.ContentSnapshot,
                            advanceTimestamp));
                    }
                }
                else
                {
                    session.Complete(answerTimestamp);
                }
            }

            changed.Add(meta.ExamId);
        }, changed, ct);

        return await ProjectStateAsync(request.SessionId, ct);
    }

    private async Task EnsureInitialTurnAsync(Guid examId, Guid enrollmentId, Guid sessionId, CancellationToken ct)
    {
        await uow.ExecuteAsync(async token =>
        {
            await ExamLock.LockAsync(db, examId, token);
            await LockEnrollmentAsync(enrollmentId, token);

            var firstQuestion = await db.SessionQuestions.IgnoreQueryFilters()
                .Where(q => q.VivaSessionId == sessionId)
                .OrderBy(q => q.Position)
                .FirstOrDefaultAsync(token);

            if (firstQuestion is null) return;

            var exists = await db.StudentAnswers.IgnoreQueryFilters()
                .AnyAsync(a => a.SessionQuestionId == firstQuestion.Id && a.FollowUpLevel == 0, token);
            if (!exists)
            {
                db.StudentAnswers.Add(new StudentAnswer(
                    firstQuestion.Id,
                    AnswerType.Main,
                    0,
                    firstQuestion.ContentSnapshot,
                    DateTimeOffset.UtcNow));
            }
        }, [], ct);
    }

    private async Task<VivaInterviewState> ProjectStateAsync(Guid sessionId, CancellationToken ct)
    {
        var header = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                            join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                            join e in db.Exams.IgnoreQueryFilters().AsNoTracking() on r.ExamId equals e.Id
                            join sub in db.Subjects.IgnoreQueryFilters().AsNoTracking() on e.SubjectId equals sub.Id
                            where s.Id == sessionId && r.StudentId == actor.Id
                            select new
                            {
                                Session = s,
                                Enrollment = r,
                                Exam = e,
                                SubjectCode = sub.Code,
                                SubjectName = sub.Name
                            }).SingleOrDefaultAsync(ct)
                     ?? throw new ResourceNotFoundException("Session not found.");

        var questions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(q => q.VivaSessionId == sessionId)
            .OrderBy(q => q.Position)
            .Select(q => new { q.Id, q.QuestionId, q.Position, q.ContentSnapshot })
            .ToArrayAsync(ct);

        var sqIds = questions.Select(q => q.Id).ToArray();
        var rawTurns = await (from a in db.StudentAnswers.IgnoreQueryFilters().AsNoTracking()
                              where sqIds.Contains(a.SessionQuestionId)
                              orderby a.AskedAt, a.FollowUpLevel
                              select new
                              {
                                  a.Id,
                                  a.SessionQuestionId,
                                  a.AnswerType,
                                  a.FollowUpLevel,
                                  a.PromptText,
                                  a.Transcript,
                                  a.AskedAt,
                                  a.AnsweredAt,
                                  HasAudio = db.AnswerAudios.IgnoreQueryFilters().Any(au => au.StudentAnswerId == a.Id),
                                  TimedOut = db.AnswerAnalyses.IgnoreQueryFilters().Any(analysis =>
                                      analysis.StudentAnswerId == a.Id && analysis.ModelName == VivaTurnDeadline.TimeoutModelName)
                              }).ToArrayAsync(ct);

        var posBySqId = questions.ToDictionary(q => q.Id, q => q.Position);
        var turns = rawTurns
            .OrderBy(t => posBySqId[t.SessionQuestionId])
            .ThenBy(t => t.FollowUpLevel)
            .Select(t => new VivaTurnItem(
                t.Id,
                t.SessionQuestionId,
                posBySqId[t.SessionQuestionId],
                t.AnswerType,
                t.FollowUpLevel,
                t.PromptText,
                t.TimedOut && (t.Transcript == VivaTurnDeadline.NoAnswerMarker ||
                    t.Transcript == "[Timed out: no transcript captured; see attached audio if available.]")
                    ? null : t.Transcript,
                t.AskedAt,
                t.AnsweredAt,
                t.HasAudio,
                t.TimedOut))
            .ToArray();

        var activeTurn = header.Session.CompletedAt.HasValue
            ? null
            : turns.FirstOrDefault(t => !t.AnsweredAt.HasValue);

        var now = DateTimeOffset.UtcNow;
        var slotStart = header.Enrollment.ScheduledAt ?? header.Session.StartedAt;
        var slotEnd = header.Exam.SlotEnd(slotStart);
        if (slotEnd > header.Exam.EndAt) slotEnd = header.Exam.EndAt;

        var remainingSlotSeconds = header.Session.CompletedAt.HasValue
            ? 0
            : Math.Max(0, (int)Math.Ceiling((slotEnd - now).TotalSeconds));

        var turnLimitSeconds = VivaTiming.CalculateTurnLimitSeconds(
            header.Exam.DurationMinutes,
            header.Exam.NumberOfMainQuestions,
            header.Exam.MaxFollowUpPerQuestion);

        var remainingTurnSeconds = activeTurn is null
            ? 0
            : Math.Min(remainingSlotSeconds, Math.Max(0, turnLimitSeconds - (int)Math.Floor((now - activeTurn.AskedAt).TotalSeconds)));

        var currentMainPos = activeTurn?.MainPosition
            ?? (turns.Length > 0 ? turns[^1].MainPosition : 1);
        var currentFollowUpLevel = activeTurn?.FollowUpLevel ?? 0;

        var safeQuestions = questions
            .Select(q => new StudentQuestion(q.QuestionId, q.Position, q.ContentSnapshot))
            .ToArray();

        return new VivaInterviewState(
            header.Session.Id,
            header.Enrollment.Id,
            header.Exam.Name,
            header.SubjectCode,
            header.SubjectName,
            header.Session.StartedAt,
            header.Session.CompletedAt,
            slotEnd,
            remainingSlotSeconds,
            turnLimitSeconds,
            remainingTurnSeconds,
            currentMainPos,
            questions.Length,
            currentFollowUpLevel,
            header.Exam.MaxFollowUpPerQuestion,
            activeTurn,
            turns,
            safeQuestions,
            await StudentGradeProjection.LoadAsync(db,sessionId,ct));
    }

    private async Task<(VivaSession Session, Guid EnrollmentId, Guid ExamId)> LoadSessionMetadataAsync(Guid sessionId, CancellationToken ct)
    {
        var item = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                          join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                          where s.Id == sessionId && r.StudentId == actor.Id
                          select new { Session = s, EnrollmentId = r.Id, r.ExamId }).SingleOrDefaultAsync(ct)
                   ?? throw new ResourceNotFoundException("Session not found.");

        return (item.Session, item.EnrollmentId, item.ExamId);
    }

    private async Task<ExamStudent> LockEnrollmentAsync(Guid id, CancellationToken ct)
    {
        var entry = await db.ExamStudents
            .FromSqlInterpolated($"SELECT * FROM ExamStudents WITH (UPDLOCK, HOLDLOCK) WHERE Id = {id}")
            .SingleAsync(r => r.StudentId == actor.Id, ct);
        await db.Entry(entry).ReloadAsync(ct);
        return entry;
    }

}

