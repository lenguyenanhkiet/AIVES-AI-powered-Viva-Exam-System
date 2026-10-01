using System.Globalization;
using AIpoweredVivaExamSystem.Application;
using AIpoweredVivaExamSystem.Persistence;
using AIpoweredVivaExamSystem.Web.Common;
using AIpoweredVivaExamSystem.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Presentation layer (MVC) reuses the Business (Application) and Data Access (Persistence) layers.
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddControllersWithViews(options => options.Filters.Add<NotFoundExceptionFilter>());
// MVC mặc định dùng cookie; fallback bảo vệ cả controller mới chưa gắn [Authorize].
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "AIVES.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/StatusCode/403";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
        options.SlidingExpiration = false;
    });
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
    .RequireAuthenticatedUser().Build());

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
// Scores are posted as "2.5" by number inputs; bind them the same way regardless of the server's locale.
app.UseRequestLocalization(options =>
{
    options.DefaultRequestCulture = new(CultureInfo.InvariantCulture);
    options.SupportedCultures = options.SupportedUICultures = [CultureInfo.InvariantCulture];
});
app.UseStatusCodePagesWithReExecute("/Home/StatusCode/{0}");
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
