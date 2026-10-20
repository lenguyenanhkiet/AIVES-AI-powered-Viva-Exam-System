using System.Net.Mail;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Exams;
using AIpoweredVivaExamSystem.Domain.Common;
using AIpoweredVivaExamSystem.Domain.Entities;
using AIpoweredVivaExamSystem.Domain.Enums;

namespace AIpoweredVivaExamSystem.Application.ExamStudents;

/// <summary>Manages existing student enrollments; scheduling remains the scheduling module's responsibility.</summary>
public sealed class ExamRosterService(IExamRosterStore store, ExamAccessGuard guard, IExamUnitOfWork unitOfWork) : IExamRosterService
{
    public async Task<IReadOnlyList<RosterEntry>> ListAsync(Guid examId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        return await store.ListAsync(examId, ct);
    }

    public async Task AddAsync(Guid examId, string studentEmail, CancellationToken ct)
    {
        // Check the actor before looking up an email so unauthorized callers cannot enumerate accounts.
        await guard.CanManageExamAsync(examId, ct);
        var email = studentEmail?.Trim().ToLowerInvariant() ?? "";
        if (email.Length > 255 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw new DomainValidationException("Vui lòng nhập email sinh viên hợp lệ.");

        await unitOfWork.ExecuteAsync(async token =>
        {
            var exam = await store.LockExamAsync(examId, token);
            await guard.CanManageExamAsync(examId, token);
            RequireDraft(exam);
            var student = await store.FindStudentAsync(email, token);
            if (student is null || student.Role != UserRole.STUDENT || student.Status != UserStatus.Active || student.DeletedAt is not null)
                throw new DomainValidationException("Chỉ thêm được tài khoản STUDENT đang hoạt động và chưa bị xóa.");
            if (await store.ContainsAsync(examId, student.Id, token))
                throw new ResourceConflictException("Sinh viên đã có trong danh sách kỳ thi.");

            store.Add(new ExamStudent(examId, student.Id));
            // A roster change invalidates scheduling previews based on the exam's RowVersion.
            exam.UpdatedAt = DateTimeOffset.UtcNow;
        }, [examId], ct);
    }

    public async Task RemoveAsync(Guid examId, Guid enrollmentId, CancellationToken ct)
    {
        await guard.CanManageExamAsync(examId, ct);
        await unitOfWork.ExecuteAsync(async token =>
        {
            var exam = await store.LockExamAsync(examId, token);
            await guard.CanManageExamAsync(examId, token);
            RequireDraft(exam);
            // Bind both IDs: an enrollment from another exam must never be removed through this route.
            var enrollment = await store.FindEnrollmentAsync(examId, enrollmentId, token)
                ?? throw new ResourceNotFoundException("Không tìm thấy sinh viên trong kỳ thi này.");
            if (await store.HasSessionAsync(enrollmentId, token))
                throw new ResourceConflictException("Không thể bỏ sinh viên đã có phiên thi.");
            store.Remove(enrollment);
            exam.UpdatedAt = DateTimeOffset.UtcNow;
        }, [examId], ct);
    }

    private static void RequireDraft(Exam exam)
    {
        if (exam.Status != ExamStatus.Draft)
            throw new ResourceConflictException("Chỉ thay đổi danh sách sinh viên khi kỳ thi ở trạng thái Draft.");
    }
}
