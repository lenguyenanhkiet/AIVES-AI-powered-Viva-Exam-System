using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Interfaces;
using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using FluentValidation;
using Xunit;

namespace AIpoweredVivaExamSystem.Tests;

public class RubricServiceTests
{
    private sealed class FakeQuestionLookup : IQuestionLookup
    {
        public HashSet<Guid> ExistingQuestions { get; } = [];
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingQuestions.Contains(id));
    }

    private sealed class FakeRubricRepository : IRubricRepository
    {
        public List<Rubric> Rubrics { get; } = [];

        public Task<Rubric?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Rubrics.FirstOrDefault(x => x.Id == id));

        public Task<Rubric?> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken) =>
            Task.FromResult(Rubrics.FirstOrDefault(x => x.QuestionId == questionId));

        public Task<bool> ExistsForQuestionAsync(Guid questionId, CancellationToken cancellationToken) =>
            Task.FromResult(Rubrics.Any(x => x.QuestionId == questionId));

        public Task<(IReadOnlyList<Rubric> Items, int TotalCount)> ListAsync(
            Guid? questionId, int page, int pageSize, CancellationToken cancellationToken)
        {
            var query = Rubrics.AsEnumerable();
            if (questionId.HasValue)
                query = query.Where(x => x.QuestionId == questionId.Value);
            var list = query.ToList();
            var paged = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(((IReadOnlyList<Rubric>)paged, list.Count));
        }

        public void Add(Rubric rubric) => Rubrics.Add(rubric);
        public void MarkUpdated(Rubric rubric) { }
        public void Remove(Rubric rubric) => Rubrics.Remove(rubric);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private readonly FakeQuestionLookup _questionLookup = new();
    private readonly FakeRubricRepository _repository = new();
    private readonly RubricService _service;

    public RubricServiceTests()
    {
        _service = new RubricService(
            _repository,
            _questionLookup,
            new CreateRubricRequestValidator(),
            new UpdateRubricRequestValidator(),
            new RubricListQueryValidator());
    }

    [Fact]
    public async Task Create_successfully_creates_rubric_and_calculates_total_score()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var request = new CreateRubricRequest(
            questionId,
            "Rubric Lập trình C#",
            "Đánh giá hiểu biết về OOP và Dependency Injection",
            [
                new CriterionRequest("Định nghĩa OOP", "Nêu đủ 4 tính chất", "OOP, Encapsulation", 3.0m, 1),
                new CriterionRequest("Dependency Injection", "Phân biệt 3 lifetime", "Scoped, Singleton, Transient", 4.5m, 2),
                new CriterionRequest("Ví dụ thực tế", "Code demo chính xác", "C#, .NET Core", 2.5m, 3)
            ]);

        var result = await _service.CreateAsync(request, default);

        Assert.NotNull(result);
        Assert.Equal(questionId, result.QuestionId);
        Assert.Equal("Rubric Lập trình C#", result.Name);
        Assert.Equal(10.0m, result.TotalScore); // 3.0 + 4.5 + 2.5 = 10.0
        Assert.Equal(3, result.Criteria.Count);
        Assert.Equal(1, result.Criteria[0].DisplayOrder);
        Assert.Equal(2, result.Criteria[1].DisplayOrder);
        Assert.Equal(3, result.Criteria[2].DisplayOrder);
    }

    [Fact]
    public async Task Create_rejects_nonexistent_question()
    {
        var nonExistentQuestionId = Guid.NewGuid();

        var request = new CreateRubricRequest(
            nonExistentQuestionId,
            "Rubric C#",
            null,
            [new CriterionRequest("Tiêu chí 1", null, null, 5.0m, 1)]);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _service.CreateAsync(request, default));
    }

    [Fact]
    public async Task Create_rejects_duplicate_rubric_for_same_question()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var request = new CreateRubricRequest(
            questionId,
            "Rubric 1",
            null,
            [new CriterionRequest("Tiêu chí 1", null, null, 5.0m, 1)]);

        await _service.CreateAsync(request, default);

        var duplicateRequest = new CreateRubricRequest(
            questionId,
            "Rubric 2",
            null,
            [new CriterionRequest("Tiêu chí 2", null, null, 10.0m, 1)]);

        await Assert.ThrowsAsync<ResourceConflictException>(() => _service.CreateAsync(duplicateRequest, default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_rejects_empty_name(string invalidName)
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var request = new CreateRubricRequest(
            questionId,
            invalidName,
            null,
            [new CriterionRequest("Tiêu chí 1", null, null, 5.0m, 1)]);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, default));
    }

    [Fact]
    public async Task Create_rejects_empty_criteria()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var request = new CreateRubricRequest(
            questionId,
            "Rubric rỗng",
            null,
            []);

        await Assert.ThrowsAsync<ValidationException>(() => _service.CreateAsync(request, default));
    }

    [Fact]
    public async Task Create_rejects_duplicate_display_order()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var request = new CreateRubricRequest(
            questionId,
            "Rubric trùng thứ tự",
            null,
            [
                new CriterionRequest("Tiêu chí 1", null, null, 5.0m, 1),
                new CriterionRequest("Tiêu chí 2", null, null, 5.0m, 1) // Trùng order 1
            ]);

        await Assert.ThrowsAsync<DomainValidationException>(() => _service.CreateAsync(request, default));
    }

    [Fact]
    public async Task Get_successfully_retrieves_rubric_by_id()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var created = await _service.CreateAsync(new CreateRubricRequest(
            questionId,
            "Rubric xem chi tiết",
            "Mô tả",
            [new CriterionRequest("Tiêu chí A", null, null, 7.5m, 1)]), default);

        var found = await _service.GetAsync(created.Id, default);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal("Rubric xem chi tiết", found.Name);
        Assert.Equal(7.5m, found.TotalScore);
    }

    [Fact]
    public async Task Get_throws_when_rubric_not_found()
    {
        var nonExistentRubricId = Guid.NewGuid();
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _service.GetAsync(nonExistentRubricId, default));
    }

    [Fact]
    public async Task GetByQuestionId_successfully_retrieves_rubric_or_returns_null_when_none_exists()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        // Chưa có rubric -> trả về null
        var beforeCreate = await _service.GetByQuestionIdAsync(questionId, default);
        Assert.Null(beforeCreate);

        // Tạo rubric
        var created = await _service.CreateAsync(new CreateRubricRequest(
            questionId,
            "Rubric theo Question",
            null,
            [new CriterionRequest("Tiêu chí 1", null, null, 10.0m, 1)]), default);

        // Đã có rubric -> trả về RubricResponse
        var afterCreate = await _service.GetByQuestionIdAsync(questionId, default);
        Assert.NotNull(afterCreate);
        Assert.Equal(created.Id, afterCreate.Id);
        Assert.Equal(10.0m, afterCreate.TotalScore);
    }

    [Fact]
    public async Task GetByQuestionId_throws_when_question_not_found()
    {
        var nonExistentQuestionId = Guid.NewGuid();
        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _service.GetByQuestionIdAsync(nonExistentQuestionId, default));
    }

    [Fact]
    public async Task Update_successfully_modifies_rubric_and_recalculates_total_score()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var created = await _service.CreateAsync(new CreateRubricRequest(
            questionId,
            "Rubric ban đầu",
            "Mô tả cũ",
            [
                new CriterionRequest("Tiêu chí 1", null, null, 4.0m, 1),
                new CriterionRequest("Tiêu chí 2", null, null, 6.0m, 2)
            ]), default);

        Assert.Equal(10.0m, created.TotalScore);

        var crit1Id = created.Criteria[0].Id;
        var crit2Id = created.Criteria[1].Id;

        // Cập nhật điểm của tiêu chí 1 lên 5.0, tiêu chí 2 lên 5.0
        var updateRequest = new UpdateRubricRequest(
            "Rubric đã cập nhật",
            "Mô tả mới",
            [
                new CriterionRequest("Tiêu chí 1 sửa", "Mô tả 1", "Khái niệm", 5.0m, 1, crit1Id),
                new CriterionRequest("Tiêu chí 2 sửa", "Mô tả 2", "Khái niệm", 5.0m, 2, crit2Id)
            ]);

        var updated = await _service.UpdateAsync(created.Id, updateRequest, default);

        Assert.Equal("Rubric đã cập nhật", updated.Name);
        Assert.Equal("Mô tả mới", updated.Description);
        Assert.Equal(10.0m, updated.TotalScore);
        Assert.Equal("Tiêu chí 1 sửa", updated.Criteria[0].Name);
    }

    [Fact]
    public async Task Update_handles_criteria_addition_and_removal_and_recalculates_total_score()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var created = await _service.CreateAsync(new CreateRubricRequest(
            questionId,
            "Rubric động",
            null,
            [
                new CriterionRequest("Tiêu chí giữ lại", null, null, 3.0m, 1),
                new CriterionRequest("Tiêu chí bị xóa", null, null, 2.0m, 2)
            ]), default);

        Assert.Equal(5.0m, created.TotalScore);
        var keptCriterionId = created.Criteria[0].Id;

        // Giữ lại tiêu chí 1, xóa tiêu chí 2, thêm tiêu chí 3 mới (Id = null) với điểm 7.0
        var updateRequest = new UpdateRubricRequest(
            "Rubric sau khi thêm bớt",
            null,
            [
                new CriterionRequest("Tiêu chí giữ lại", null, null, 3.0m, 1, keptCriterionId),
                new CriterionRequest("Tiêu chí mới thêm", "Mô tả mới", "Concept mới", 7.0m, 2, null)
            ]);

        var updated = await _service.UpdateAsync(created.Id, updateRequest, default);

        Assert.Equal(2, updated.Criteria.Count);
        Assert.Equal(10.0m, updated.TotalScore); // 3.0 + 7.0 = 10.0
        Assert.Equal("Tiêu chí giữ lại", updated.Criteria[0].Name);
        Assert.Equal("Tiêu chí mới thêm", updated.Criteria[1].Name);
    }

    [Fact]
    public async Task Update_throws_when_rubric_not_found()
    {
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateRubricRequest(
            "Tên mới",
            null,
            [new CriterionRequest("Tiêu chí", null, null, 5.0m, 1)]);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _service.UpdateAsync(nonExistentId, request, default));
    }

    [Fact]
    public async Task Delete_removes_rubric_successfully()
    {
        var questionId = Guid.NewGuid();
        _questionLookup.ExistingQuestions.Add(questionId);

        var created = await _service.CreateAsync(new CreateRubricRequest(
            questionId,
            "Rubric cần xóa",
            null,
            [new CriterionRequest("Tiêu chí", null, null, 5.0m, 1)]), default);

        await _service.DeleteAsync(created.Id, default);

        await Assert.ThrowsAsync<ResourceNotFoundException>(() => _service.GetAsync(created.Id, default));
        Assert.Null(await _service.GetByQuestionIdAsync(questionId, default));
    }
}
