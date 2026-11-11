using System.Text.Json;
using System.Text.RegularExpressions;
using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace AIpoweredVivaExamSystem.Persistence.Repositories;

public sealed class VivaGradingStore(
    ApplicationDbContext db,
    IExamUnitOfWork uow,
    IVivaAiGrader aiGrader,
    ICurrentUser actor) : IVivaGradingStore
{
    public async Task<Guid> FindExamIdBySessionAsync(Guid sessionId, CancellationToken ct) =>
        await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
               join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
               where s.Id == sessionId
               select (Guid?)r.ExamId).SingleOrDefaultAsync(ct)
        ?? throw new ResourceNotFoundException("Không tìm thấy phiên thi vấn đáp.");

    public async Task<ExamGradingOverview> GetExamOverviewAsync(Guid examId, CancellationToken ct)
    {
        var examHeader = await (from e in db.Exams.IgnoreQueryFilters().AsNoTracking()
                                join sub in db.Subjects.IgnoreQueryFilters().AsNoTracking() on e.SubjectId equals sub.Id
                                where e.Id == examId && e.DeletedAt == null
                                select new
                                {
                                    Exam = new ExamSummary(e.Id, e.SubjectId, e.Name, e.Status, e.StartAt, e.EndAt),
                                    SubjectCode = sub.Code,
                                    SubjectName = sub.Name
                                }).SingleOrDefaultAsync(ct)
                         ?? throw new ResourceNotFoundException("Không tìm thấy kỳ thi.");

        var roster = await (from r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking()
                            join u in db.Users.IgnoreQueryFilters().AsNoTracking() on r.StudentId equals u.Id
                            where r.ExamId == examId && u.DeletedAt == null
                            orderby r.ScheduledAt == null, r.ScheduledAt, u.FullName, u.Email
                            select new
                            {
                                EnrollmentId = r.Id,
                                r.StudentId,
                                StudentName = u.FullName,
                                StudentEmail = u.Email,
                                r.ScheduledAt,
                                Session = db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                                    .Where(s => s.ExamStudentId == r.Id)
                                    .Select(s => new { s.Id, s.StartedAt, s.CompletedAt })
                                    .FirstOrDefault()
                            }).ToArrayAsync(ct);

        var sessionIds = roster.Where(r => r.Session is not null).Select(r => r.Session!.Id).ToArray();
        var questions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(sq => sessionIds.Contains(sq.VivaSessionId))
            .Select(sq => new { sq.Id, sq.VivaSessionId, sq.RubricSnapshot })
            .ToArrayAsync(ct);

        var sqIds = questions.Select(q => q.Id).ToArray();
        var aiGradings = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
            .Where(g => sqIds.Contains(g.SessionQuestionId))
            .Select(g => new { g.SessionQuestionId, g.AiScore, g.MaxScore })
            .ToArrayAsync(ct);

        var reviews = await db.TeacherReviews.IgnoreQueryFilters().AsNoTracking()
            .Where(tr => sqIds.Contains(tr.SessionQuestionId))
            .Select(tr => new { tr.SessionQuestionId, tr.FinalScore, tr.MaxScore })
            .ToArrayAsync(ct);

        var aiBySq = aiGradings.ToDictionary(x => x.SessionQuestionId);
        var reviewBySq = reviews.ToDictionary(x => x.SessionQuestionId);
        var questionsBySession = questions.GroupBy(q => q.VivaSessionId).ToDictionary(g => g.Key, g => g.ToArray());

        var rows = new List<ExamStudentGradingRow>(roster.Length);
        var completedCount = 0;
        var fullyReviewedCount = 0;

        foreach (var item in roster)
        {
            if (item.Session is null)
            {
                rows.Add(new ExamStudentGradingRow(
                    item.EnrollmentId,
                    null,
                    item.StudentId,
                    item.StudentName,
                    item.StudentEmail,
                    item.ScheduledAt,
                    SessionState.NotStarted,
                    VivaGradingStatus.NotStarted,
                    0,
                    0,
                    0,
                    null,
                    null,
                    null,
                    null));
                continue;
            }

            var sessionState = item.Session.CompletedAt.HasValue ? SessionState.Completed : SessionState.InProgress;
            if (sessionState == SessionState.Completed) completedCount++;

            var sessionQuestions = questionsBySession.GetValueOrDefault(item.Session.Id) ?? [];
            var totalQ = sessionQuestions.Length;
            var aiCount = 0;
            var reviewCount = 0;
            decimal totalMax = 0m;
            decimal sumAi = 0m;
            decimal sumFinal = 0m;

            foreach (var sq in sessionQuestions)
            {
                var qMax = ParseSnapshotMaxScore(sq.RubricSnapshot);
                if (aiBySq.TryGetValue(sq.Id, out var ai))
                {
                    aiCount++;
                    sumAi += ai.AiScore;
                    qMax = ai.MaxScore;
                }
                if (reviewBySq.TryGetValue(sq.Id, out var rev))
                {
                    reviewCount++;
                    sumFinal += rev.FinalScore;
                    qMax = rev.MaxScore;
                }

                totalMax += qMax;
            }

            var gradingStatus = (totalQ, aiCount, reviewCount) switch
            {
                (0, _, _) => VivaGradingStatus.NotStarted,
                (_, _, var r) when r == totalQ => VivaGradingStatus.Reviewed,
                (_, _, > 0) => VivaGradingStatus.PartiallyReviewed,
                (_, var a, 0) when a == totalQ => VivaGradingStatus.AiSuggested,
                _ => VivaGradingStatus.PendingAiGrading
            };

            if (gradingStatus == VivaGradingStatus.Reviewed) fullyReviewedCount++;

            var isFullyReviewed = reviewCount == totalQ && totalQ > 0;
            decimal? totalAiNullable = aiCount > 0 ? decimal.Round(sumAi, 2, MidpointRounding.AwayFromZero) : null;
            decimal? totalFinalNullable = isFullyReviewed ? decimal.Round(sumFinal, 2, MidpointRounding.AwayFromZero) : null;
            decimal? normFinal10 = isFullyReviewed && totalMax > 0
                ? decimal.Round((sumFinal / totalMax) * 10m, 2, MidpointRounding.AwayFromZero)
                : null;

            rows.Add(new ExamStudentGradingRow(
                item.EnrollmentId,
                item.Session.Id,
                item.StudentId,
                item.StudentName,
                item.StudentEmail,
                item.ScheduledAt,
                sessionState,
                gradingStatus,
                totalQ,
                aiCount,
                reviewCount,
                totalMax > 0 ? totalMax : null,
                totalAiNullable,
                totalFinalNullable,
                normFinal10));
        }

        return new ExamGradingOverview(
            examHeader.Exam,
            examHeader.SubjectCode,
            examHeader.SubjectName,
            roster.Length,
            completedCount,
            fullyReviewedCount,
            rows);
    }

    public async Task<VivaSessionGradingSheet> GetSessionGradingSheetAsync(Guid sessionId, CancellationToken ct)
    {
        var header = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                            join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                            join u in db.Users.IgnoreQueryFilters().AsNoTracking() on r.StudentId equals u.Id
                            join e in db.Exams.IgnoreQueryFilters().AsNoTracking() on r.ExamId equals e.Id
                            join sub in db.Subjects.IgnoreQueryFilters().AsNoTracking() on e.SubjectId equals sub.Id
                            where s.Id == sessionId
                            select new
                            {
                                SessionId = s.Id,
                                EnrollmentId = r.Id,
                                ExamId = e.Id,
                                ExamName = e.Name,
                                SubjectCode = sub.Code,
                                SubjectName = sub.Name,
                                StudentId = u.Id,
                                StudentName = u.FullName,
                                StudentEmail = u.Email,
                                s.StartedAt,
                                s.CompletedAt
                            }).SingleOrDefaultAsync(ct)
                     ?? throw new ResourceNotFoundException("Không tìm thấy phiên thi vấn đáp.");

        var questions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(sq => sq.VivaSessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToArrayAsync(ct);

        var sqIds = questions.Select(q => q.Id).ToArray();

        var answers = await db.StudentAnswers.IgnoreQueryFilters().AsNoTracking()
            .Where(a => sqIds.Contains(a.SessionQuestionId))
            .OrderBy(a => a.AskedAt).ThenBy(a => a.FollowUpLevel)
            .ToArrayAsync(ct);

        var answerIds = answers.Select(a => a.Id).ToArray();
        var audios = await db.AnswerAudios.IgnoreQueryFilters().AsNoTracking()
            .Where(au => answerIds.Contains(au.StudentAnswerId))
            .ToDictionaryAsync(au => au.StudentAnswerId, ct);

        var analyses = await db.AnswerAnalyses.IgnoreQueryFilters().AsNoTracking()
            .Where(an => answerIds.Contains(an.StudentAnswerId))
            .ToDictionaryAsync(an => an.StudentAnswerId, ct);

        var aiGradings = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
            .Include(g => g.Criteria)
            .Where(g => sqIds.Contains(g.SessionQuestionId))
            .ToDictionaryAsync(g => g.SessionQuestionId, ct);

        var reviews = await (from tr in db.TeacherReviews.IgnoreQueryFilters().AsNoTracking()
                             join lec in db.Users.IgnoreQueryFilters().AsNoTracking() on tr.LecturerId equals lec.Id
                             where sqIds.Contains(tr.SessionQuestionId)
                             select new { Review = tr, LecturerName = lec.FullName })
                            .ToDictionaryAsync(x => x.Review.SessionQuestionId, ct);

        var questionItems = new List<SessionQuestionGradingItem>(questions.Length);
        decimal totalMaxScore = 0m;
        decimal sumAiScore = 0m;
        decimal sumFinalScore = 0m;
        var aiCount = 0;
        var reviewedCount = 0;

        foreach (var sq in questions)
        {
            var parsedRubric = ParseRubricSnapshot(sq.RubricSnapshot);
            var qTurnsRaw = answers.Where(a => a.SessionQuestionId == sq.Id).OrderBy(a => a.FollowUpLevel).ToArray();

            var turnItems = new List<GradedTurnItem>(qTurnsRaw.Length);
            var signalInputs = new List<(DateTimeOffset AskedAt, DateTimeOffset? AnsweredAt, int? AudioDurationSeconds, string? Transcript)>();

            foreach (var turn in qTurnsRaw)
            {
                audios.TryGetValue(turn.Id, out var audio);
                analyses.TryGetValue(turn.Id, out var analysis);

                int? respSec = null;
                if (turn.AnsweredAt.HasValue)
                {
                    respSec = audio?.DurationSeconds is > 0
                        ? audio.DurationSeconds
                        : Math.Max(1, (int)Math.Round((turn.AnsweredAt.Value - turn.AskedAt).TotalSeconds));
                }

                var wordCount = string.IsNullOrWhiteSpace(turn.Transcript)
                    ? 0
                    : Regex.Matches(turn.Transcript, @"[\p{L}\p{N}]+").Count;

                turnItems.Add(new GradedTurnItem(
                    turn.Id,
                    turn.AnswerType,
                    turn.FollowUpLevel,
                    turn.PromptText,
                    turn.Transcript,
                    turn.AskedAt,
                    turn.AnsweredAt,
                    respSec,
                    wordCount,
                    audio is not null,
                    analysis?.MissingConcepts,
                    analysis?.AnalysisSummary));

                signalInputs.Add((turn.AskedAt, turn.AnsweredAt, audio?.DurationSeconds, turn.Transcript));
            }

            var (totalSec, totalWords, wpm, _) = VivaGradingService.CalculateFluencySignals(signalInputs);

            AiGradingDetails? aiDetails = null;
            if (aiGradings.TryGetValue(sq.Id, out var ai))
            {
                aiCount++;
                sumAiScore += ai.AiScore;
                aiDetails = new AiGradingDetails(
                    ai.Id,
                    ai.AiScore,
                    ai.MaxScore,
                    ai.AiComment,
                    ai.Strengths,
                    ai.Weaknesses,
                    ai.MissingPoints,
                    ai.TotalResponseSeconds,
                    ai.WordsPerMinute,
                    ai.FluencyNote,
                    ai.ModelName,
                    ai.UpdatedAt ?? ai.CreatedAt,
                    ai.Criteria
                        .OrderBy(c => c.DisplayOrder)
                        .Select(c => new AiCriterionEvaluation(c.RubricCriterionId, c.CriterionName, c.MaxScore, c.Score, c.Evidence, c.Comment, c.DisplayOrder))
                        .ToArray());
            }

            TeacherReviewDetails? reviewDetails = null;
            if (reviews.TryGetValue(sq.Id, out var revEntry))
            {
                reviewedCount++;
                sumFinalScore += revEntry.Review.FinalScore;
                reviewDetails = new TeacherReviewDetails(
                    revEntry.Review.Id,
                    revEntry.Review.LecturerId,
                    revEntry.LecturerName,
                    revEntry.Review.AiScoreSnapshot,
                    revEntry.Review.TeacherScore,
                    revEntry.Review.FinalScore,
                    revEntry.Review.MaxScore,
                    revEntry.Review.Comment,
                    revEntry.Review.ReviewedAt);
            }

            var effectiveMax = reviewDetails?.MaxScore ?? aiDetails?.MaxScore ?? parsedRubric.MaxScore;
            totalMaxScore += effectiveMax;

            questionItems.Add(new SessionQuestionGradingItem(
                sq.Id,
                sq.QuestionId,
                sq.Position,
                sq.ContentSnapshot,
                parsedRubric.ExpectedAnswer,
                parsedRubric.RubricName,
                effectiveMax,
                parsedRubric.Criteria,
                turnItems,
                totalSec,
                totalWords,
                wpm,
                aiDetails,
                reviewDetails));
        }

        var isFullyReviewed = reviewedCount == questions.Length && questions.Length > 0;
        decimal? totalAiNullable = aiCount > 0 ? decimal.Round(sumAiScore, 2, MidpointRounding.AwayFromZero) : null;
        decimal? totalFinalNullable = isFullyReviewed ? decimal.Round(sumFinalScore, 2, MidpointRounding.AwayFromZero) : null;
        decimal? normAi10 = aiCount > 0 && totalMaxScore > 0
            ? decimal.Round((sumAiScore / totalMaxScore) * 10m, 2, MidpointRounding.AwayFromZero)
            : null;
        decimal? normFinal10 = isFullyReviewed && totalMaxScore > 0
            ? decimal.Round((sumFinalScore / totalMaxScore) * 10m, 2, MidpointRounding.AwayFromZero)
            : null;

        return new VivaSessionGradingSheet(
            header.SessionId,
            header.EnrollmentId,
            header.ExamId,
            header.ExamName,
            header.SubjectCode,
            header.SubjectName,
            header.StudentId,
            header.StudentName,
            header.StudentEmail,
            header.StartedAt,
            header.CompletedAt,
            totalMaxScore,
            totalAiNullable,
            totalFinalNullable,
            normAi10,
            normFinal10,
            aiCount,
            reviewedCount,
            questions.Length,
            questionItems);
    }

    public Task<VivaSessionGradingSheet> GenerateAiGradingsAsync(Guid sessionId, bool forceRegrade, CancellationToken ct) =>
        GenerateAiGradingsCoreAsync(sessionId, forceRegrade, onlyQuestionId: null, ct);

    public Task<VivaSessionGradingSheet> GenerateQuestionAiGradingAsync(Guid sessionId, Guid sessionQuestionId, CancellationToken ct) =>
        GenerateAiGradingsCoreAsync(sessionId, forceRegrade: true, onlyQuestionId: sessionQuestionId, ct);

    public async Task<int> GenerateExamAiGradingsAsync(Guid examId, CancellationToken ct)
    {
        var sessionIdsWithMissingAi = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                                             join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                                             where r.ExamId == examId &&
                                                   db.SessionQuestions.IgnoreQueryFilters().Any(sq =>
                                                       sq.VivaSessionId == s.Id &&
                                                       !db.AiGradings.IgnoreQueryFilters().Any(g => g.SessionQuestionId == sq.Id))
                                             select s.Id).Distinct().ToArrayAsync(ct);

        var gradedSessions = 0;
        foreach (var sid in sessionIdsWithMissingAi)
        {
            await GenerateAiGradingsCoreAsync(sid, forceRegrade: false, onlyQuestionId: null, ct);
            gradedSessions++;
        }

        return gradedSessions;
    }

    private async Task<VivaSessionGradingSheet> GenerateAiGradingsCoreAsync(
        Guid sessionId,
        bool forceRegrade,
        Guid? onlyQuestionId,
        CancellationToken ct)
    {
        var meta = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                          join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                          join e in db.Exams.IgnoreQueryFilters().AsNoTracking() on r.ExamId equals e.Id
                          join sub in db.Subjects.IgnoreQueryFilters().AsNoTracking() on e.SubjectId equals sub.Id
                          where s.Id == sessionId
                          select new { s.Id, ExamId = e.Id, SubjectName = sub.Name }).SingleOrDefaultAsync(ct)
                   ?? throw new ResourceNotFoundException("Không tìm thấy phiên thi vấn đáp.");

        var questions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(sq => sq.VivaSessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToArrayAsync(ct);

        if (onlyQuestionId is { } qId && questions.All(q => q.Id != qId))
            throw new ResourceNotFoundException("Không tìm thấy câu hỏi trong phiên thi này.");

        var sqIds = questions.Select(q => q.Id).ToArray();
        var existingGradingIds = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
            .Where(g => sqIds.Contains(g.SessionQuestionId))
            .Select(g => g.SessionQuestionId)
            .ToArrayAsync(ct);

        var targetQuestions = onlyQuestionId.HasValue
            ? questions.Where(q => q.Id == onlyQuestionId.Value).ToArray()
            : (forceRegrade
                ? questions
                : questions.Where(q => !existingGradingIds.Contains(q.Id)).ToArray());

        if (targetQuestions.Length == 0)
        {
            return await GetSessionGradingSheetAsync(sessionId, ct);
        }

        var answers = await db.StudentAnswers.IgnoreQueryFilters().AsNoTracking()
            .Where(a => sqIds.Contains(a.SessionQuestionId))
            .OrderBy(a => a.AskedAt).ThenBy(a => a.FollowUpLevel)
            .ToArrayAsync(ct);

        var answerIds = answers.Select(a => a.Id).ToArray();
        var audios = await db.AnswerAudios.IgnoreQueryFilters().AsNoTracking()
            .Where(au => answerIds.Contains(au.StudentAnswerId))
            .ToDictionaryAsync(au => au.StudentAnswerId, ct);

        var decisions = new List<(Guid SessionQuestionId, int TotalSec, int Wpm, string DefaultFluency, AiGradingDecision Decision)>();

        foreach (var sq in targetQuestions)
        {
            var parsedRubric = ParseRubricSnapshot(sq.RubricSnapshot);
            var qTurns = answers.Where(a => a.SessionQuestionId == sq.Id).OrderBy(a => a.FollowUpLevel).ToArray();
            var signalInputs = qTurns.Select(t => (
                t.AskedAt,
                t.AnsweredAt,
                audios.TryGetValue(t.Id, out var au) ? au.DurationSeconds : null,
                t.Transcript)).ToArray();

            var (totalSec, _, wpm, defaultFluency) = VivaGradingService.CalculateFluencySignals(signalInputs);

            var history = qTurns
                .Where(t => t.AnsweredAt.HasValue && !string.IsNullOrWhiteSpace(t.Transcript))
                .Select(t => new ConversationTurn(t.AnswerType, t.FollowUpLevel, t.PromptText, t.Transcript!))
                .ToArray();

            var context = new AiGradingEvaluationContext(
                meta.SubjectName,
                sq.ContentSnapshot,
                sq.RubricSnapshot,
                parsedRubric.MaxScore,
                totalSec,
                wpm,
                history);

            var decision = await aiGrader.GradeQuestionAsync(context, ct);
            decisions.Add((sq.Id, totalSec, wpm, defaultFluency, decision));
        }

        await uow.ExecuteAsync(async token =>
        {
            foreach (var (sqId, totalSec, wpm, defaultFluency, decision) in decisions)
            {
                var existing = await db.AiGradings.IgnoreQueryFilters()
                    .Include(g => g.Criteria)
                    .SingleOrDefaultAsync(g => g.SessionQuestionId == sqId, token);

                var fluencyNote = string.IsNullOrWhiteSpace(decision.FluencyNote) ? defaultFluency : decision.FluencyNote;

                if (existing is null)
                {
                    existing = new AiGrading(
                        sqId,
                        decision.AiScore,
                        decision.MaxScore,
                        decision.AiComment,
                        decision.Strengths,
                        decision.Weaknesses,
                        decision.MissingPoints,
                        totalSec,
                        wpm,
                        fluencyNote,
                        decision.ModelName);
                    db.AiGradings.Add(existing);
                }
                else
                {
                    existing.Update(
                        decision.AiScore,
                        decision.MaxScore,
                        decision.AiComment,
                        decision.Strengths,
                        decision.Weaknesses,
                        decision.MissingPoints,
                        totalSec,
                        wpm,
                        fluencyNote,
                        decision.ModelName);

                    var oldCriteria = await db.AiGradingCriteria.IgnoreQueryFilters()
                        .Where(c => c.AiGradingId == existing.Id)
                        .ToListAsync(token);
                    db.AiGradingCriteria.RemoveRange(oldCriteria);
                }

                var order = 1;
                foreach (var crit in decision.Criteria)
                {
                    db.AiGradingCriteria.Add(new AiGradingCriterion(
                        existing.Id,
                        crit.RubricCriterionId,
                        crit.CriterionName,
                        crit.MaxScore,
                        crit.Score,
                        crit.Evidence,
                        crit.Comment,
                        crit.DisplayOrder > 0 ? crit.DisplayOrder : order));
                    order++;
                }
            }
        }, [meta.ExamId], ct);

        return await GetSessionGradingSheetAsync(sessionId, ct);
    }

    public async Task<VivaSessionGradingSheet> SaveTeacherReviewAsync(SaveTeacherReviewRequest request, CancellationToken ct)
    {
        var examId = await FindExamIdBySessionAsync(request.SessionId, ct);

        var sq = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.SessionQuestionId && x.VivaSessionId == request.SessionId, ct)
            ?? throw new ResourceNotFoundException("Không tìm thấy câu hỏi trong phiên thi này.");

        var maxScore = ParseSnapshotMaxScore(sq.RubricSnapshot);

        await uow.ExecuteAsync(async token =>
        {
            var aiGrading = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
                .SingleOrDefaultAsync(g => g.SessionQuestionId == sq.Id, token);

            var effectiveMax = aiGrading?.MaxScore ?? maxScore;
            var aiSnapshot = aiGrading?.AiScore;
            var now = DateTimeOffset.UtcNow;

            var existingReview = await db.TeacherReviews.IgnoreQueryFilters()
                .SingleOrDefaultAsync(r => r.SessionQuestionId == sq.Id, token);

            if (existingReview is null)
            {
                db.TeacherReviews.Add(new TeacherReview(
                    sq.Id,
                    actor.Id,
                    request.FinalScore,
                    effectiveMax,
                    aiSnapshot,
                    request.Comment,
                    now));
            }
            else
            {
                existingReview.UpdateReview(
                    actor.Id,
                    request.FinalScore,
                    effectiveMax,
                    aiSnapshot ?? existingReview.AiScoreSnapshot,
                    request.Comment,
                    now);
            }
        }, [examId], ct);

        return await GetSessionGradingSheetAsync(request.SessionId, ct);
    }

    public async Task<VivaSessionGradingSheet> ApproveAllAiScoresAsync(Guid sessionId, string? comment, CancellationToken ct)
    {
        var examId = await FindExamIdBySessionAsync(sessionId, ct);
        await GenerateAiGradingsAsync(sessionId, forceRegrade: false, ct);

        await uow.ExecuteAsync(async token =>
        {
            var sqIds = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
                .Where(sq => sq.VivaSessionId == sessionId)
                .Select(sq => sq.Id)
                .ToArrayAsync(token);

            var aiGradings = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
                .Where(g => sqIds.Contains(g.SessionQuestionId))
                .ToListAsync(token);

            var existingReviews = await db.TeacherReviews.IgnoreQueryFilters()
                .Where(r => sqIds.Contains(r.SessionQuestionId))
                .ToDictionaryAsync(r => r.SessionQuestionId, token);

            var now = DateTimeOffset.UtcNow;
            foreach (var ai in aiGradings)
            {
                var effectiveComment = !string.IsNullOrWhiteSpace(comment)
                    ? comment.Trim()
                    : (existingReviews.TryGetValue(ai.SessionQuestionId, out var prev) && !string.IsNullOrWhiteSpace(prev.Comment)
                        ? prev.Comment
                        : "Giảng viên xác nhận đồng ý với điểm và nhận xét gợi ý của AI.");

                if (existingReviews.TryGetValue(ai.SessionQuestionId, out var existing))
                {
                    existing.UpdateReview(actor.Id, ai.AiScore, ai.MaxScore, ai.AiScore, effectiveComment, now);
                }
                else
                {
                    db.TeacherReviews.Add(new TeacherReview(
                        ai.SessionQuestionId,
                        actor.Id,
                        ai.AiScore,
                        ai.MaxScore,
                        ai.AiScore,
                        effectiveComment,
                        now));
                }
            }
        }, [examId], ct);

        return await GetSessionGradingSheetAsync(sessionId, ct);
    }

    public async Task<StudentSessionGradingResult?> GetStudentResultAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await (from s in db.VivaSessions.IgnoreQueryFilters().AsNoTracking()
                             join r in db.ExamStudents.IgnoreQueryFilters().AsNoTracking() on s.ExamStudentId equals r.Id
                             where s.Id == sessionId && r.StudentId == actor.Id
                             select s).SingleOrDefaultAsync(ct)
                      ?? throw new ResourceNotFoundException("Session not found.");

        var questions = await db.SessionQuestions.IgnoreQueryFilters().AsNoTracking()
            .Where(sq => sq.VivaSessionId == sessionId)
            .OrderBy(sq => sq.Position)
            .ToArrayAsync(ct);

        if (questions.Length == 0) return null;

        var sqIds = questions.Select(q => q.Id).ToArray();
        var aiGradings = await db.AiGradings.IgnoreQueryFilters().AsNoTracking()
            .Where(g => sqIds.Contains(g.SessionQuestionId))
            .ToDictionaryAsync(g => g.SessionQuestionId, ct);

        var reviews = await db.TeacherReviews.IgnoreQueryFilters().AsNoTracking()
            .Where(r => sqIds.Contains(r.SessionQuestionId))
            .ToDictionaryAsync(r => r.SessionQuestionId, ct);

        if (aiGradings.Count == 0 && reviews.Count == 0 && !session.CompletedAt.HasValue)
            return null;

        decimal totalMax = 0m;
        decimal sumAi = 0m;
        decimal sumFinal = 0m;
        var aiCount = 0;
        var reviewCount = 0;
        var items = new List<StudentQuestionResultItem>(questions.Length);

        foreach (var sq in questions)
        {
            aiGradings.TryGetValue(sq.Id, out var ai);
            reviews.TryGetValue(sq.Id, out var rev);

            var maxScore = rev?.MaxScore ?? ai?.MaxScore ?? ParseSnapshotMaxScore(sq.RubricSnapshot);
            totalMax += maxScore;

            if (ai is not null)
            {
                aiCount++;
                sumAi += ai.AiScore;
            }

            if (rev is not null)
            {
                reviewCount++;
                sumFinal += rev.FinalScore;
            }

            items.Add(new StudentQuestionResultItem(
                sq.Position,
                sq.ContentSnapshot,
                maxScore,
                ai?.AiScore,
                ai?.AiComment,
                ai?.Strengths,
                ai?.Weaknesses,
                ai?.MissingPoints,
                ai?.TotalResponseSeconds ?? 0,
                ai?.WordsPerMinute ?? 0,
                ai?.FluencyNote,
                rev?.FinalScore,
                rev?.Comment,
                rev?.ReviewedAt));
        }

        var isFullyReviewed = reviewCount == questions.Length && questions.Length > 0;
        decimal? totalAiNullable = aiCount > 0 ? decimal.Round(sumAi, 2, MidpointRounding.AwayFromZero) : null;
        decimal? totalFinalNullable = isFullyReviewed ? decimal.Round(sumFinal, 2, MidpointRounding.AwayFromZero) : null;
        decimal? normAi10 = aiCount > 0 && totalMax > 0
            ? decimal.Round((sumAi / totalMax) * 10m, 2, MidpointRounding.AwayFromZero)
            : null;
        decimal? normFinal10 = isFullyReviewed && totalMax > 0
            ? decimal.Round((sumFinal / totalMax) * 10m, 2, MidpointRounding.AwayFromZero)
            : null;

        return new StudentSessionGradingResult(
            sessionId,
            totalMax,
            totalAiNullable,
            totalFinalNullable,
            normAi10,
            normFinal10,
            isFullyReviewed,
            items);
    }

    private static decimal ParseSnapshotMaxScore(string rubricSnapshotJson) =>
        VivaGradingService.ParseRubricSnapshot(rubricSnapshotJson).MaxScore;

    private static (string RubricName, string? ExpectedAnswer, decimal MaxScore, IReadOnlyList<RubricCriterionSnapshotItem> Criteria) ParseRubricSnapshot(
        string rubricSnapshotJson) =>
        VivaGradingService.ParseRubricSnapshot(rubricSnapshotJson);
}
