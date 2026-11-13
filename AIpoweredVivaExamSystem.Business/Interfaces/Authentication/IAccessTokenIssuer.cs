namespace AIpoweredVivaExamSystem.Business.Authentication;

public interface IAccessTokenIssuer
{
    AccessToken Issue(AuthenticatedUser user);
}
