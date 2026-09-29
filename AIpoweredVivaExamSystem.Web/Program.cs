using System.Globalization;
using AIpoweredVivaExamSystem.Application;
using AIpoweredVivaExamSystem.Persistence;
using AIpoweredVivaExamSystem.Web.Common;

var builder = WebApplication.CreateBuilder(args);

// Presentation layer (MVC) reuses the Business (Application) and Data Access (Persistence) layers.
builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddControllersWithViews(options => options.Filters.Add<NotFoundExceptionFilter>());

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

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
