using System.Text.Json;
using backend.Shared.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace backend.Api.Application.Exceptions;

public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (HttpProblemException ex)
        {
            await WriteProblem(context, ex.StatusCode, ex.Title, ex.Detail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            await WriteProblem(
                context,
                StatusCodes.Status500InternalServerError,
                "Server error",
                "An unexpected error occurred."
            );
        }
    }

    private static async Task WriteProblem(
        HttpContext context,
        int statusCode,
        string title,
        string? detail,
        Dictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = $"https://httpstatuses.com/{statusCode}",
            Instance = context.Request.Path
        };

        if (errors != null)
        {
            problem.Extensions["errors"] = errors;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
