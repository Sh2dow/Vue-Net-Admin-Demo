using backend.Domain.Data;
using backend.Domain.Models;
using backend.Tasks.Dtos;
using backend.Tasks.Requests.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Functions.Functions;

public class TasksFunctions
{
    private readonly IMediator _mediator;
    private readonly TasksDbContext _db;
    private readonly ILogger<TasksFunctions> _logger;

    public TasksFunctions(IMediator mediator, TasksDbContext db, ILogger<TasksFunctions> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    [Function("GetTasks")]
    public async Task<IActionResult> GetTasks(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks")] HttpRequest req,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing GetTasks request");

        var pageNumber = int.TryParse(req.Query["pageNumber"], out var pn) ? pn : 1;
        var pageSize = int.TryParse(req.Query["pageSize"], out var ps) ? ps : 20;

        var query = new GetTasksQuery(pageNumber, Math.Clamp(pageSize, 1, 100));
        var result = await _mediator.Send(query, ct);
        return new OkObjectResult(result);
    }

    [Function("GetTaskById")]
    public async Task<IActionResult> GetTaskById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "tasks/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing GetTaskById request for {TaskId}", id);

        var query = new GetTaskByIdQuery(id);
        var dto = await _mediator.Send(query, ct);

        if (dto is null)
        {
            return new NotFoundObjectResult(new { error = "Task not found." });
        }

        return new OkObjectResult(dto);
    }

    [Function("CreateTask")]
    public async Task<IActionResult> CreateTask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tasks")] HttpRequest req,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing CreateTask request");

        var command = await req.ReadFromJsonAsync<CreateTaskCommand>(ct);
        if (command is null)
        {
            return new BadRequestObjectResult(new { error = "Invalid request body." });
        }

        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            return new CreatedResult($"/api/tasks/{result.Value!.Id}", result.Value);
        }

        var firstError = result.Errors.FirstOrDefault();
        return firstError?.Code switch
        {
            "not_found" => new NotFoundObjectResult(new { error = firstError.Message }),
            "forbidden" => new ObjectResult(new { error = firstError.Message }) { StatusCode = StatusCodes.Status403Forbidden },
            _ => new BadRequestObjectResult(new { errors = result.Errors })
        };
    }

    [Function("DeleteTask")]
    public async Task<IActionResult> DeleteTask(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "tasks/{id:guid}")] HttpRequest req,
        Guid id,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing DeleteTask request for {TaskId}", id);

        var command = new DeleteTaskCommand(id);
        var result = await _mediator.Send(command, ct);

        if (result.IsSuccess)
        {
            return new NoContentResult();
        }

        var firstError = result.Errors.FirstOrDefault();
        return firstError?.Code switch
        {
            "not_found" => new NotFoundObjectResult(new { error = firstError.Message }),
            _ => new BadRequestObjectResult(new { errors = result.Errors })
        };
    }

    [Function("TasksHealth")]
    public IActionResult Health(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        return new OkObjectResult(new { status = "healthy", service = "functions" });
    }
}
