using backend.Domain.Data;
using backend.Shared.Application.Users;
using backend.Tasks.Dtos;
using backend.Tasks.Mappers;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tasks.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public class TasksController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ISender sender, ILogger<TasksController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskItemDto>> GetTaskById(Guid id, CancellationToken ct)
    {
        var task = await _sender.Send(new GetTaskByIdQuery(id), ct);
        return task is null ? NotFound() : Ok(task);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItemDto>>> GetTasks([FromQuery] GetTasksQuery query, CancellationToken ct)
    {
        var hasAuth = Request.Headers.TryGetValue("Authorization", out var authVal);
        _logger.LogInformation(
            "GetTasks called. QueryString={QueryString}, HasAuthHeader={HasAuth}, AuthPrefix={AuthPrefix}, IsAuthenticated={IsAuth}, UserName={UserName}",
            Request.QueryString.Value,
            hasAuth,
            hasAuth ? authVal.ToString()[..Math.Min(20, authVal.ToString().Length)] + "..." : null,
            User.Identity?.IsAuthenticated,
            User.Identity?.Name);
        var tasks = await _sender.Send(query, ct);
        return Ok(tasks);
    }

    [HttpGet("debug/auth")]
    public ActionResult<object> DebugAuth()
    {
        var authHeader = Request.Headers.TryGetValue("Authorization", out var h) ? h.ToString() : null;
        var identity = User.Identity;
        return Ok(new
        {
            hasAuthHeader = !string.IsNullOrWhiteSpace(authHeader),
            authHeaderPrefix = authHeader?.Length > 20 ? authHeader[..20] + "..." : authHeader,
            isAuthenticated = identity?.IsAuthenticated ?? false,
            name = identity?.Name,
            subject = User.FindFirst("sub")?.Value,
            preferredUsername = User.FindFirst("preferred_username")?.Value,
            email = User.FindFirst("email")?.Value,
            roles = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList(),
            asUserId = Request.Query["asUserId"].FirstOrDefault(),
        });
    }

    [HttpPost]
    public async Task<ActionResult<TaskItemDto>> CreateTask(CreateTaskCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetTaskById), new { id = result.Value.Id }, result.Value) : BadRequest(result.Errors);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TaskItemDto>> UpdateTask(Guid id, UpdateTaskCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(new UpdateTaskCommand(id, command.Title, command.Description, command.Status, command.Priority), ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new DeleteTaskCommand(id), ct);
        return result.Value ? NoContent() : NotFound();
    }

    [HttpPost("{taskId}/comments")]
    public async Task<ActionResult<TaskCommentDto>> AddTaskComment(Guid taskId, AddTaskCommentCommand command, CancellationToken ct)
    {
        var result = await _sender.Send(new AddTaskCommentCommand(taskId, command.Content), ct);
        return result.IsSuccess ? CreatedAtAction(nameof(GetTaskById), new { id = taskId }, result.Value) : BadRequest(result.Errors);
    }

    [HttpGet("debugroles")]
    public ActionResult<IReadOnlyList<string>> GetDebugRoles()
    {
        var roles = User.FindAll("realm_access.roles").Select(c => c.Value).ToList();
        return Ok(roles);
    }
}
