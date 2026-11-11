using System.Net;
using System.Text;
using AIpoweredVivaExamSystem.Application.VivaSessions;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;
using AIpoweredVivaExamSystem.Infrastructure.VivaSessions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public sealed class VivaGradingTests
{
    [Fact]
    public void AiGrading_and_TeacherReview_enforce_score_bounds_and_human_in_the_loop_invariants()
    {
        var sqId = Guid.NewGuid();
        var lecturerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var aiGrading = new AiGrading(
            sqId,
            aiScore: 7.5m,
            maxScore: 10m,
            aiComment: "  Nắm vững ý chính.  ",
            strengths: "  Giải thích rõ IoC.  ",
            weaknesses: "  Thiếu ví dụ cụ thể.  ",
            missingPoints: "  Scoped vs Transient  ",
            totalResponseSeconds: 45,
            wordsPerMinute: 110,
            fluencyNote: "  Trôi chảy  ",
            modelName: "gpt-4o-mini");

        Assert.Equal(7.5m, aiGrading.AiScore);
        Assert.Equal(10m, aiGrading.MaxScore);
        Assert.Equal("Nắm vững ý chính.", aiGrading.AiComment);
        Assert.Equal("Giải thích rõ IoC.", aiGrading.Strengths);
        Assert.Equal(45, aiGrading.TotalResponseSeconds);
        Assert.Equal(110, aiGrading.WordsPerMinute);

        // Score out of bounds rejected
        Assert.Throws<DomainValidationException>(() =>
            new AiGrading(sqId, -0.5m, 10m, null, null, null, null, 10, 80, null, "gpt-4o-mini"));
        Assert.Throws<DomainValidationException>(() =>
            new AiGrading(sqId, 10.5m, 10m, null, null, null, null, 10, 80, null, "gpt-4o-mini"));

        var criterion = new AiGradingCriterion(aiGrading.Id, Guid.NewGuid(), "Khái niệm DI", 5m, 4.25m, "\"DI giảm phụ thuộc\"", "Đạt yêu cầu", 1);
        Assert.Equal(4.25m, criterion.Score);
        Assert.Equal(5m, criterion.MaxScore);
        Assert.Throws<DomainValidationException>(() =>
            new AiGradingCriterion(aiGrading.Id, null, "Khái niệm DI", 5m, 6m, null, null, 1));

        // TeacherReview records lecturer decision alongside AI score snapshot without mutating AiGrading
        var review = new TeacherReview(sqId, lecturerId, finalScore: 8.25m, maxScore: 10m, aiScoreSnapshot: aiGrading.AiScore, comment: "  Sinh viên trả lời hỏi xoáy tốt.  ", reviewedAt: now);
        Assert.Equal(8.25m, review.FinalScore);
        Assert.Equal(8.25m, review.TeacherScore);
        Assert.Equal(7.5m, review.AiScoreSnapshot);
        Assert.Equal("Sinh viên trả lời hỏi xoáy tốt.", review.Comment);
        Assert.Equal(7.5m, aiGrading.AiScore); // AI score remains intact

        // TeacherReview rejects score exceeding maxScore or negative
        Assert.Throws<DomainValidationException>(() =>
            review.UpdateReview(lecturerId, 11m, 10m, 7.5m, null, now));
        Assert.Throws<DomainValidationException>(() =>
            review.UpdateReview(lecturerId, -1m, 10m, 7.5m, null, now));
    }

    [Fact]
    public void CalculateFluencySignals_computes_response_time_and_wpm_accurately()
    {
        var t0 = DateTimeOffset.UtcNow;
        var turns = new (DateTimeOffset AskedAt, DateTimeOffset? AnsweredAt, int? AudioDurationSeconds, string? Transcript)[]
        {
            (t0, t0.AddSeconds(30), 20, "Dependency Injection giúp tách biệt việc khởi tạo đối tượng khỏi logic nghiệp vụ chính của lớp."),
            (t0.AddSeconds(35), t0.AddSeconds(65), 10, "Trong ASP.NET Core có ba vòng đời chính là Transient Scoped và Singleton.")
        };

        var (totalSeconds, totalWords, wpm, note) = VivaGradingService.CalculateFluencySignals(turns);
        Assert.Equal(30, totalSeconds); // 20s + 10s
        Assert.True(totalWords >= 25);
        Assert.Equal((int)Math.Round(totalWords * 60.0 / 30), wpm);
        Assert.False(string.IsNullOrWhiteSpace(note));
    }

    [Fact]
    public void FallbackGrader_scores_rubric_criteria_and_identifies_strengths_and_missing_points()
    {
        var rubricJson = """
        {
          "Name": "Rubric DI & Lifetimes",
          "TotalScore": 10,
          "ExpectedAnswer": "Giải thích IoC container, Constructor Injection và phân biệt Transient, Scoped, Singleton.",
          "Criteria": [
            {
              "Id": "11111111-1111-1111-1111-111111111111",
              "Name": "Nguyên lý IoC & Constructor Injection",
              "ExpectedConcepts": "IoC container, Constructor Injection, interface",
              "MaxScore": 5,
              "DisplayOrder": 1
            },
            {
              "Id": "22222222-2222-2222-2222-222222222222",
              "Name": "Vòng đời dịch vụ (Service Lifetimes)",
              "ExpectedConcepts": "Transient, Scoped, Singleton",
              "MaxScore": 5,
              "DisplayOrder": 2
            }
          ]
        }
        """;

        var context = new AiGradingEvaluationContext(
            SubjectName: "PRN222",
            QuestionContent: "Giải thích Dependency Injection và các vòng đời dịch vụ trong ASP.NET Core.",
            RubricSnapshotJson: rubricJson,
            MaxScore: 10m,
            TotalResponseSeconds: 35,
            WordsPerMinute: 95,
            History:
            [
                new ConversationTurn(
                    AnswerType.Main,
                    0,
                    "Giải thích Dependency Injection và các vòng đời dịch vụ trong ASP.NET Core.",
                    "Dependency Injection sử dụng IoC container để tiêm phụ thuộc thông qua interface và Constructor Injection giúp giảm coupling.")
            ]);

        var decision = OpenAiVivaGrader.EvaluateByRubricFallback(context);

        Assert.Equal(10m, decision.MaxScore);
        Assert.Equal(2, decision.Criteria.Count);
        // Criterion 1 covered all 3 concepts => full 5.0 score
        Assert.Equal(5m, decision.Criteria[0].Score);
        // Criterion 2 missed Transient, Scoped, Singleton => lower score
        Assert.True(decision.Criteria[1].Score < 2.5m);
        Assert.True(decision.AiScore > 5m && decision.AiScore < 8m);
        Assert.Contains("IoC container", decision.Strengths);
        Assert.Contains("Transient", decision.MissingPoints);
    }

    [Fact]
    public async Task OpenAiVivaGrader_parses_structured_json_grading_response()
    {
        var aiPayload = """
        {
          "choices": [
            {
              "finish_reason": "stop",
              "message": {
                "content": "{\"aiScore\":8.5,\"aiComment\":\"Sinh viên nắm chắc kiến thức nền tảng.\",\"strengths\":\"Giải thích tốt Clean Architecture.\",\"weaknesses\":\"Ví dụ còn ngắn.\",\"missingPoints\":\"Chưa nêu rõ Domain Events.\",\"fluencyNote\":\"Trình bày mạch lạc ~110 từ/phút.\",\"criteria\":[{\"criterionId\":\"11111111-1111-1111-1111-111111111111\",\"criterionName\":\"Kiến trúc phân tầng\",\"maxScore\":10,\"score\":8.5,\"evidence\":\"Tầng Domain không phụ thuộc tầng ngoài\",\"comment\":\"Trả lời đúng trọng tâm\"}]}"
              }
            }
          ]
        }
        """;

        using var http = new HttpClient(new StubHttpMessageHandler(aiPayload));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
                ["Ai:Model"] = "gpt-4o-mini"
            })
            .Build();

        var grader = new OpenAiVivaGrader(http, config);
        var rubricJson = """
        {
          "Name": "Clean Architecture Rubric",
          "TotalScore": 10,
          "Criteria": [
            {
              "Id": "11111111-1111-1111-1111-111111111111",
              "Name": "Kiến trúc phân tầng",
              "MaxScore": 10,
              "DisplayOrder": 1
            }
          ]
        }
        """;

        var ctx = new AiGradingEvaluationContext(
            "PRN222",
            "Trình bày nguyên tắc Dependency Rule trong Clean Architecture.",
            rubricJson,
            10m,
            25,
            110,
            [new ConversationTurn(AnswerType.Main, 0, "Prompt", "Tầng Domain không phụ thuộc tầng ngoài.")]);

        var decision = await grader.GradeQuestionAsync(ctx, CancellationToken.None);
        Assert.Equal(8.5m, decision.AiScore);
        Assert.Equal(10m, decision.MaxScore);
        Assert.Equal("gpt-4o-mini", decision.ModelName);
        Assert.Single(decision.Criteria);
        Assert.Equal(8.5m, decision.Criteria[0].Score);
        Assert.Equal("Giải thích tốt Clean Architecture.", decision.Strengths);
    }

    [Fact]
    public async Task OpenAiVivaGrader_matches_criteria_by_id_even_when_returned_out_of_order()
    {
        var aiPayload = """
        {
          "choices": [
            {
              "finish_reason": "stop",
              "message": {
                "content": "{\"aiScore\":7.0,\"aiComment\":\"Khá\",\"criteria\":[{\"criterionId\":\"22222222-2222-2222-2222-222222222222\",\"criterionName\":\"Tiêu chí 2\",\"maxScore\":4,\"score\":3.5,\"evidence\":\"B\",\"comment\":\"C2\"},{\"criterionId\":\"11111111-1111-1111-1111-111111111111\",\"criterionName\":\"Tiêu chí 1\",\"maxScore\":6,\"score\":5.5,\"evidence\":\"A\",\"comment\":\"C1\"}]}"
              }
            }
          ]
        }
        """;

        using var http = new HttpClient(new StubHttpMessageHandler(aiPayload));
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ai:ApiKey"] = "test-key",
                ["Ai:Model"] = "gpt-4o-mini"
            })
            .Build();

        var grader = new OpenAiVivaGrader(http, config);
        var rubricJson = """
        {
          "Name": "Rubric 2 tiêu chí",
          "TotalScore": 10,
          "Criteria": [
            {
              "Id": "11111111-1111-1111-1111-111111111111",
              "Name": "Tiêu chí 1",
              "MaxScore": 6,
              "DisplayOrder": 1
            },
            {
              "Id": "22222222-2222-2222-2222-222222222222",
              "Name": "Tiêu chí 2",
              "MaxScore": 4,
              "DisplayOrder": 2
            }
          ]
        }
        """;

        var ctx = new AiGradingEvaluationContext(
            "PRN222",
            "Câu hỏi kiểm thử",
            rubricJson,
            10m,
            30,
            100,
            [new ConversationTurn(AnswerType.Main, 0, "Prompt", "Câu trả lời đầy đủ.")]);

        var decision = await grader.GradeQuestionAsync(ctx, CancellationToken.None);
        Assert.Equal(2, decision.Criteria.Count);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), decision.Criteria[0].RubricCriterionId);
        Assert.Equal(6m, decision.Criteria[0].MaxScore);
        Assert.Equal(5.5m, decision.Criteria[0].Score);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), decision.Criteria[1].RubricCriterionId);
        Assert.Equal(4m, decision.Criteria[1].MaxScore);
        Assert.Equal(3.5m, decision.Criteria[1].Score);
        Assert.Equal(9.0m, decision.AiScore);
    }

    private sealed class StubHttpMessageHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
