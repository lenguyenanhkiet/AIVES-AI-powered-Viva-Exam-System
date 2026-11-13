using AIpoweredVivaExamSystem.Business;
using System.Globalization;
using AIpoweredVivaExamSystem.Presentation.Common;
using AIpoweredVivaExamSystem.Presentation.Authentication;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using AIpoweredVivaExamSystem.Business.Authentication;
using AIpoweredVivaExamSystem.Business.Exams;
using AIpoweredVivaExamSystem.Business.Grading;
using AIpoweredVivaExamSystem.Presentation.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBusiness();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new NotFoundExceptionFilter());
    // Accept bookmarked conventional routes while links use each page's explicit route.
    void AddLegacyRoute(string page, string route) => options.Conventions.AddPageRouteModelConvention(page, model =>
        model.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel { Template = route, SuppressLinkGeneration = true }
        }));
    AddLegacyRoute("/Home/Index", "/Home");
    AddLegacyRoute("/Home/Index", "/Home/Index/{id?}");
    AddLegacyRoute("/Access/Index", "/Access/Index/{id?}");
    AddLegacyRoute("/Documents/Index", "/Documents/Index/{id?}");
    AddLegacyRoute("/ExamFoundation/Index", "/ExamFoundation/Index/{id?}");
    AddLegacyRoute("/ExamPools/Index", "/ExamPools");
    AddLegacyRoute("/ExamStudents/Index", "/ExamStudents/Index/{id?}");
    AddLegacyRoute("/Exams/Index", "/Exams/Index/{id?}");
    AddLegacyRoute("/QuestionImports/Index", "/QuestionImports/Index/{id?}");
    AddLegacyRoute("/Questions/Index", "/Questions/Index/{id?}");
    AddLegacyRoute("/Subjects/Index", "/Subjects/Index/{id?}");
    AddLegacyRoute("/Topics/Index", "/Topics/Index/{id?}");
    AddLegacyRoute("/VivaSessions/Index", "/VivaSessions/Index/{id?}");
});
builder.Services.AddAivesAuthentication();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddSingleton<ExamConnections>();
builder.Services.AddScoped<IExamNotifier, SignalRExamNotifier>();
builder.Services.AddSingleton<StudentResultConnections>();
builder.Services.AddScoped<IStudentResultNotifier, SignalRStudentResultNotifier>();

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
app.MapHub<AIpoweredVivaExamSystem.Presentation.Realtime.ExamHub>("/hubs/exams", options => options.CloseOnAuthenticationExpiration = true);
app.MapHub<AIpoweredVivaExamSystem.Presentation.Realtime.StudentResultHub>("/hubs/student-results", options => options.CloseOnAuthenticationExpiration = true);

app.MapRazorPages().WithStaticAssets();

app.Run();
