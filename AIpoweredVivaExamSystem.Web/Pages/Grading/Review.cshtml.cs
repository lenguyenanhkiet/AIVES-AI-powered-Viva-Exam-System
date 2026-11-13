using AIpoweredVivaExamSystem.Application.Common;
using AIpoweredVivaExamSystem.Application.Grading;
using AIpoweredVivaExamSystem.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AIpoweredVivaExamSystem.Web.Pages.Grading;
[Authorize(Roles = "ADMIN,LECTURER")]
[ResponseCache(NoStore=true, Location=ResponseCacheLocation.None)]
public sealed class ReviewModel(GradingService grading) : PageModel
{
    public SessionReview Vm { get; private set; } = default!;
    [BindProperty] public GradeInput Input { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        try { await LoadAsync(id, true, ct); return Page(); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
    }
    private async Task LoadAsync(Guid id, bool initialise, CancellationToken ct)
    {
        Vm = await grading.ReviewAsync(id,ct);
        if (initialise) Input = new GradeInput { Version=Vm.Version, OverallComment=Vm.OverallComment, Questions=QuestionsFromReview() };
        else if (!Input.Questions.Select(q=>q.SessionQuestionId).SequenceEqual(Vm.Questions.Select(q=>q.SessionQuestionId)) ||
            Input.Questions.Where((q,i)=>!q.Criteria.Select(c=>c.CriterionId).SequenceEqual(Vm.Questions[i].Criteria.Select(c=>c.Id))).Any())
        {
            // A partial/tampered form still needs a complete shape for indexed Razor fields.
            // Preserve values with valid identities; move errors to the summary before reindexing.
            var posted=Input.Questions;
            Input.Questions=QuestionsFromReview();
            foreach(var q in Input.Questions)
            {
                var value=posted.FirstOrDefault(p=>p.SessionQuestionId==q.SessionQuestionId);
                if(value is null) continue;
                q.Comment=value.Comment;
                foreach(var c in q.Criteria) c.Score=value.Criteria.FirstOrDefault(p=>p.CriterionId==c.CriterionId)?.Score;
            }
            foreach(var key in ModelState.Keys.Where(k=>k.StartsWith("Input.Questions",StringComparison.Ordinal)).ToArray())
            {
                foreach(var error in ModelState[key]!.Errors) ModelState.AddModelError("",string.IsNullOrEmpty(error.ErrorMessage)?"Dữ liệu điểm không hợp lệ.":error.ErrorMessage);
                ModelState.Remove(key);
            }
        }
    }
    private List<QuestionGradeInput> QuestionsFromReview()=>Vm.Questions.Select(q=>new QuestionGradeInput {
        SessionQuestionId=q.SessionQuestionId,Comment=q.LecturerComment,
        Criteria=q.Criteria.Select(c=>new CriterionGradeInput {CriterionId=c.Id,Score=c.LecturerScore}).ToList() }).ToList();
    public Task<IActionResult> OnPostSaveAsync(Guid id, CancellationToken ct) => SaveAsync(id,false,ct);
    public Task<IActionResult> OnPostFinaliseAsync(Guid id, CancellationToken ct) => SaveAsync(id,true,ct);
    private async Task<IActionResult> SaveAsync(Guid id,bool finalise,CancellationToken ct)
    {
        try {
            if (ModelState.IsValid) { await grading.SaveAsync(id,Input,finalise,ct); return RedirectToPage(new {id}); }
        } catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (GradingValidationException ex) { foreach (var e in ex.Errors) ModelState.AddModelError(e.Key,e.Value); }
        catch (Exception ex) when (ex is DomainValidationException or ResourceConflictException) { ModelState.AddModelError("",ex.Message); }
        try { await LoadAsync(id,false,ct); return Page(); } catch (UnauthorizedAccessException) { return StatusCode(403); }
    }
    public async Task<IActionResult> OnPostReopenAsync(Guid id,string version,CancellationToken ct)
    {
        try { await grading.ReopenAsync(id,version,ct); return RedirectToPage(new {id}); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (Exception ex) when (ex is ResourceConflictException or DomainValidationException) { ModelState.AddModelError("",ex.Message); await LoadAsync(id,true,ct); return Page(); }
    }
    public async Task<IActionResult> OnPostSuggestAsync(Guid id,Guid questionId,bool regenerate=false,CancellationToken ct=default)
    {
        try { return new JsonResult(await grading.SuggestAsync(id,questionId,regenerate,ct)); }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
        catch (ResourceConflictException ex) { return new JsonResult(new { error=ex.Message }) { StatusCode=409 }; }
    }
    public async Task<IActionResult> OnGetSuggestionAsync(Guid id,Guid questionId,CancellationToken ct)
    {
        try {
            var question=(await grading.ReviewAsync(id,ct)).Questions.SingleOrDefault(q=>q.SessionQuestionId==questionId);
            return question is null?NotFound():new JsonResult(question);
        }
        catch (UnauthorizedAccessException) { return StatusCode(403); }
    }
}
