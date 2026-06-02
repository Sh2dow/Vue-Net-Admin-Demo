using backend.Domain.Models;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using ResultError = backend.Shared.Application.Results.ResultError;

namespace backend.Api.Application.Results;

public static class ValidationProblemDetailsExtensions
{
    public static ValidationProblemDetails ToValidationProblemDetails(this ResultError error)
    {
        var properties = string.IsNullOrWhiteSpace(error.Field)
            ? new Dictionary<string, string[]> { { "General", new[] { error.Message } } }
            : new Dictionary<string, string[]> { { error.Field!, new[] { error.Message } } };

        return new ValidationProblemDetails(properties)
        {
            Status = 400,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };
    }

    public static ValidationProblemDetails ToValidationProblemDetails(this IEnumerable<ResultError> errors)
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

    public static ValidationProblemDetails ToValidationProblemDetails(this IEnumerable<Domain.Models.ResultError> errors)
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

    public static ValidationProblemDetails ToValidationProblemDetails<T>(this Shared.Application.Results.Result<T> result)
    {
        if (!result.IsSuccess || result.Errors.Count == 0)
            return new ValidationProblemDetails()
            {
                Status = 400,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred."
            };

        return result.Errors.ToValidationProblemDetails();
    }

    public static ValidationProblemDetails ToValidationProblemDetails<T>(this CommandResult<T> result)
    {
        if (!result.IsSuccess || result.Errors.Count == 0)
            return new ValidationProblemDetails()
            {
                Status = 400,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred."
            };

        return result.Errors.ToValidationProblemDetails();
    }

    public static ValidationProblemDetails ToValidationProblemDetails<T>(this DomainResult<T> result)
    {
        if (!result.IsSuccess || result.Errors.Count == 0)
            return new ValidationProblemDetails()
            {
                Status = 400,
                Title = "Validation failed",
                Detail = "One or more validation errors occurred."
            };

        return result.Errors.ToValidationProblemDetails();
    }

    public static ValidationResult ToFluentValidationResult<T>(this Shared.Application.Results.Result<T> result)
    {
        var failures = result.Errors
            .Where(x => x.Code == "validation")
            .Select(x => new FluentValidation.Results.ValidationFailure(
                string.IsNullOrWhiteSpace(x.Field) ? "General" : x.Field!,
                x.Message));

        return new ValidationResult(failures);
    }

    public static ValidationResult ToFluentValidationResult<T>(this CommandResult<T> result)
    {
        var failures = result.Errors
            .Where(x => x.Code == "validation")
            .Select(x => new FluentValidation.Results.ValidationFailure(
                string.IsNullOrWhiteSpace(x.Field) ? "General" : x.Field!,
                x.Message));

        return new ValidationResult(failures);
    }

    public static ValidationResult ToFluentValidationResult<T>(this DomainResult<T> result)
    {
        var failures = result.Errors
            .Where(x => x.Code == "validation")
            .Select(x => new FluentValidation.Results.ValidationFailure(
                string.IsNullOrWhiteSpace(x.Field) ? "General" : x.Field!,
                x.Message));

        return new ValidationResult(failures);
    }
}
