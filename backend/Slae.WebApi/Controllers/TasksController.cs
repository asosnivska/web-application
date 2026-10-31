using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Slae.Application.Interfaces;
using Slae.Application.Services;
using Slae.WebApi.Dtos;
using Slae.WebApi.Extensions;

namespace Slae.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ITaskStore _store;

    public TasksController(ITaskStore store)
    {
        _store = store;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateTaskRequest req, CancellationToken ct)
    {
        var p = req.ToParams();

        var errors = TaskParamsValidator.Validate(p);
        if (errors.Count > 0)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["params"] = errors.ToArray() }));
        }

        var result = await _store.CreateAsync(User.GetUserId(), p, ct);

        switch (result.Status)
        {
            case CreateStatus.LimitReached:
                return Problem(statusCode: StatusCodes.Status429TooManyRequests,
                    title: "Task limit reached",
                    detail: $"Active tasks: {result.Active}, limit: {result.Limit}.");

            case CreateStatus.UserNotFound:
                return Unauthorized();

            default:
                return Accepted($"/api/tasks/{result.TaskId}", new { id = result.TaskId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken ct = default)
    {
        var items = await _store.ListAsync(User.GetUserId(), skip, take, ct);
        return Ok(items.Select(TaskMapping.ToResponse));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var task = await _store.GetAsync(User.GetUserId(), id, ct);
        return task is null ? NotFound() : Ok(TaskMapping.ToResponse(task));
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var result = await _store.CancelAsync(User.GetUserId(), id, ct);

        return result switch
        {
            CancelResult.CanceledImmediately => Ok(new { status = "canceled" }),
            CancelResult.CancelRequested => Ok(new { status = "cancel_requested" }),
            CancelResult.AlreadyFinished => Problem(statusCode: StatusCodes.Status409Conflict, title: "Task is already finished"),
            _ => NotFound()
        };
    }
}