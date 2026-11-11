using System.Security.Cryptography;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class VivaInterviewStore(
    ApplicationDbContext db,
    IExamUnitOfWork uow,
    IVivaAiExaminer examiner,
    IAnswerAudioStorage audioStorage,
    ICurrentUser actor) : IVivaInterviewStore
{
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

    public async Task<VivaInterviewState> SubmitAnswerAsync(SubmitAnswerRequest request, CancellationToken ct)
    {
        var meta = await LoadSessionMetadataAsync(request.SessionId, ct);
        var transcript = DomainRules.RequiredText(request.Transcript, nameof(request.Transcript), 8000);

        byte[]? validAudio = null;
        string? validMime = null;
        string? audioHash = null;
        string? savedAudioPath = null;

        if (request.AudioBytes is { Length: > 0 } bytes)
        {
            if (bytes.Length > 20 * 1024 * 1024)
                throw new DomainValidationException("File ghi âm vượt quá giới hạn 20 MB.");
            validMime = NormalizeAudioMime(request.AudioMimeType);
            validAudio = bytes;
            audioHash = Convert.ToHexString(SHA256.HashData(bytes));
            savedAudioPath = await audioStorage.SaveAudioAsync(request.SessionId, request.TurnId, bytes, validMime, ct);
        }

        var changed = new List<Guid>();
        await uow.ExecuteAsync(async token =>
        {
            var exam = await ExamLock.LockAsync(db, meta.ExamId, token);
            var enrollment = await LockEnrollmentAsync(meta.EnrollmentId, token);
            var session = await db.VivaSessions.IgnoreQueryFilters().SingleAsync(s => s.Id == request.SessionId, token);

            if (session.CompletedAt.HasValue)
                throw new DomainValidationException("Phiên thi đã kết thúc, không thể nộp thêm câu trả lời.");

            var now = DateTimeOffset.UtcNow;
            if (exam.Status != ExamStatus.Ongoing || enrollment.ScheduledAt is not { } slotStart ||
                now < slotStart || now >= exam.SlotEnd(slotStart) || now >= exam.EndAt)
            {
                throw new DomainValidationException("Thời gian ca thi đã kết thúc hoặc kỳ thi không còn mở.");
            }

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

            // Idempotent handling: if this turn was already answered in a concurrent request, return cleanly.
            if (currentTurn.AnsweredAt.HasValue)
                return;

            var activeUnanswered = turns.FirstOrDefault(t => !t.AnsweredAt.HasValue);
            if (activeUnanswered is not null && activeUnanswered.Id != currentTurn.Id)
                throw new ResourceConflictException("Lượt câu hỏi hiện tại đã thay đổi. Hãy tải lại phiên thi.");

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
            var remainingSlotSeconds = (exam.SlotEnd(slotStart) - answerTimestamp).TotalSeconds;
            var canAskFollowUp = currentTurn.FollowUpLevel < exam.MaxFollowUpPerQuestion && remainingSlotSeconds >= 15;

            var subjectName = await db.Subjects.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.Id == exam.SubjectId)
                .Select(s => s.Name)
                .SingleAsync(token);

            var questionTurns = turns
                .Where(t => t.SessionQuestionId == currentQuestion.Id && t.AnsweredAt.HasValue)
                .OrderBy(t => t.FollowUpLevel)
                .Select(t => new ConversationTurn(t.AnswerType, t.FollowUpLevel, t.PromptText, t.Transcript ?? ""))
                .ToArray();

            var context = new ExaminerEvaluationContext(
                subjectName,
                currentQuestion.ContentSnapshot,
                currentQuestion.RubricSnapshot,
                currentTurn.FollowUpLevel,
                exam.MaxFollowUpPerQuestion,
                questionTurns);

            var decision = await examiner.EvaluateAndFollowUpAsync(context, token);
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

            var advanceToNextMain = true;
            if (followUpPrompt is not null)
            {
                var nextFollowUp = new StudentAnswer(
                    currentQuestion.Id,
                    AnswerType.FollowUp,
                    currentTurn.FollowUpLevel + 1,
                    followUpPrompt,
                    answerTimestamp,
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
                            answerTimestamp));
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
                                  HasAudio = db.AnswerAudios.IgnoreQueryFilters().Any(au => au.StudentAnswerId == a.Id)
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
                t.Transcript,
                t.AskedAt,
                t.AnsweredAt,
                t.HasAudio))
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

        var turnLimitSeconds = VivaInterviewService.CalculateTurnLimitSeconds(
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
            safeQuestions);
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

    private static string NormalizeAudioMime(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType)) return "audio/webm";
        var normalized = mimeType.Split(';')[0].Trim().ToLowerInvariant();
        return normalized switch
        {
            "audio/webm" or "audio/wav" or "audio/wave" or "audio/x-wav" or "audio/mp4" or "audio/mpeg" or "audio/mp3" or "audio/ogg" => normalized,
            _ => throw new DomainValidationException("Định dạng âm thanh không hợp lệ (chỉ hỗ trợ webm, wav, mp4, mp3, ogg).")
        };
    }
}

