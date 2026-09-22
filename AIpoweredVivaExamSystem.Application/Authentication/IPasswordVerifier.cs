using AIpoweredVivaExamSystem.Domain.Entities;
namespace AIpoweredVivaExamSystem.Application.Authentication;

// Thay adapter này nếu nhóm chọn thuật toán hash khác cho đăng ký.
public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}
