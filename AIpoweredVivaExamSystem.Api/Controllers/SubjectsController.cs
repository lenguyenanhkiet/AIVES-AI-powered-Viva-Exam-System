using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Subjects;
using AIpoweredVivaExamSystem.Application.Subjects.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class SubjectsController(ISubjectService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubjectResponse>> Create(CreateSubjectRequest request, CancellationToken cancellationToken)
    {
        var subject = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = subject.Id }, subject);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubjectResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedResponse<SubjectResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<SubjectResponse>>> List(
        [FromQuery] SubjectListQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<SubjectResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SubjectResponse>> Update(
        Guid id, UpdateSubjectRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
