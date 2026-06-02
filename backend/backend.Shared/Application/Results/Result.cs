using FluentValidation.Results;

namespace backend.Shared.Application.Results;

public sealed record ResultError(string Code, string Message, string? Field = null);

public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<ResultError> Errors { get; }

    public static Result<T> Success(T value) => new(true, value, []);

    public static Result<T> NotFound(string message = "Resource not found.")
        => new(false, default, [new ResultError("not_found", message)]);

    public static Result<T> Conflict(string message)
        => new(false, default, [new ResultError("conflict", message)]);

    public static Result<T> Forbidden(string message = "Forbidden.")
        => new(false, default, [new ResultError("forbidden", message)]);

    public static Result<T> Unauthorized(string message = "Unauthorized.")
        => new(false, default, [new ResultError("unauthorized", message)]);

    public static Result<T> Validation(IEnumerable<ResultError> errors)
        => new(false, default, errors.ToList());

    public static Result<T> Validation(IEnumerable<ValidationFailure> failures)
        => new(
            false,
            default,
            failures
                .Select(x => new ResultError("validation", x.ErrorMessage, x.PropertyName))
                .ToList()
        );

    public static Result<T> ValidationFromDomainErrors(IEnumerable<Domain.Models.ResultError> errors)
        => new(
            false,
            default,
            errors.Select(e => new ResultError(e.Code, e.Message, e.Field)).ToList()
        );
}
