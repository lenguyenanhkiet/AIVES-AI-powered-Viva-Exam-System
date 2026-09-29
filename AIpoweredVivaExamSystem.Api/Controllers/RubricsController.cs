using AIpoweredVivaExamSystem.Application.Rubrics;
using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Api.Controllers;

[ApiController]
[Route("api/rubrics")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class RubricsController(IRubricService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<RubricResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RubricResponse>> Create(CreateRubricRequest request, CancellationToken cancellationToken)
    {
        var rubric = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = rubric.Id }, rubric);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<RubricResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RubricResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedResponse<RubricResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<RubricResponse>>> List(
        [FromQuery] RubricListQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<RubricResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RubricResponse>> Update(
        Guid id, UpdateRubricRequest request, CancellationToken cancellationToken) =>
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
