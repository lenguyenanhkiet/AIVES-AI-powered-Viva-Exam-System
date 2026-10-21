namespace AIpoweredVivaExamSystem.Web.Pages.QuestionImports;

internal sealed record PendingImport(Guid UserId, string Token, byte[] Content, string FileName);
