using AIpoweredVivaExamSystem.Application.Authentication;
using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using AIpoweredVivaExamSystem.Web.Common;

namespace AIpoweredVivaExamSystem.Web.Pages.Access;

[Authorize(Roles = "ADMIN")]
public sealed class IndexModel(AccessService access) : ContentPageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken ct) => RenderContent("/Pages/Access/_IndexContent.cshtml", await access.OverviewAsync(ct));

    public IActionResult OnPost() => NotFound();
}
