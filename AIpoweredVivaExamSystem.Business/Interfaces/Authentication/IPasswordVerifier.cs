using AIpoweredVivaExamSystem.DataAccess.Entities;

namespace AIpoweredVivaExamSystem.Business.Authentication;

public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}
