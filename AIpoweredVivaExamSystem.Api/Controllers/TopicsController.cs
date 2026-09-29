using AIpoweredVivaExamSystem.Application.Rubrics.DTOs;
using AIpoweredVivaExamSystem.Application.Topics;
using AIpoweredVivaExamSystem.Application.Topics.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace AIpoweredVivaExamSystem.Api.Controllers;

[ApiController]
[Route("api/topics")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
public sealed class TopicsController(ITopicService service) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<TopicResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TopicResponse>> Create(CreateTopicRequest request, CancellationToken cancellationToken)
    {
        var topic = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = topic.Id }, topic);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<TopicResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TopicResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpGet]
    [ProducesResponseType<PagedResponse<TopicResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<TopicResponse>>> List(
        [FromQuery] TopicListQuery query, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(query, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType<TopicResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TopicResponse>> Update(
        Guid id, UpdateTopicRequest request, CancellationToken cancellationToken) =>
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
