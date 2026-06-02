using backend.Shared.Application.Results;
using Microsoft.AspNetCore.Mvc;

namespace backend.Api.Application.Results;

public static class ResultActionResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this ControllerBase controller,
        Result<T> result,
        Func<T, IActionResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            if (result.Value is null) return controller.Ok();
            return onSuccess is null ? controller.Ok(result.Value) : onSuccess(result.Value);
        }

        var first = result.Errors.FirstOrDefault();
        if (first is null)
        {
            return controller.BadRequest();
        }

        return first.Code switch
        {
            "validation" => controller.BadRequest(BuildValidationProblem(result.Errors)),
            "not_found" => controller.NotFound(BuildProblemDetails(404, "Not Found", first.Message)),
            "conflict" => controller.Conflict(BuildProblemDetails(409, "Conflict", first.Message)),
            "forbidden" => controller.StatusCode(403, BuildProblemDetails(403, "Forbidden", first.Message)),
            "unauthorized" => controller.Unauthorized(BuildProblemDetails(401, "Unauthorized", first.Message)),
            _ => controller.BadRequest(BuildProblemDetails(400, "Bad Request", first.Message))
        };
    }

    private static ValidationProblemDetails BuildValidationProblem(IEnumerable<ResultError> errors)
    {
        var grouped = errors
            .Where(x => x.Code == "validation")
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Field) ? "General" : x.Field!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Message).Distinct().ToArray());

        return new ValidationProblemDetails(grouped)
        {
            Status = 400,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };
    }

    private static ProblemDetails BuildProblemDetails(int status, string title, string detail) =>
        new()
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{status}"
        };
}
