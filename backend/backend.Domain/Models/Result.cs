namespace backend.Domain.Models;

public readonly record struct DomainUnit;

// Use backend.Application.Results.ResultError instead of defining our own
// This requires adding a reference to backend.Shared, but it avoids circular dependency
// because backend.Shared already references backend.Domain
public readonly record struct ResultError(string Code, string Message, string? Field = null);

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
}

public sealed class DomainResult<T>
{
    private DomainResult(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<ResultError> Errors { get; }

    public static DomainResult<T> Success(T value) => new(true, value, []);

    public static DomainResult<T> Failure(IEnumerable<ResultError> errors) => new(false, default, errors.ToList());

    public static DomainResult<T> Failure(ResultError error) => new(false, default, [error]);
}

public sealed class CommandResult<T>
{
    private CommandResult(bool isSuccess, T? value, IReadOnlyList<ResultError> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<ResultError> Errors { get; }

    public static CommandResult<T> Success(T value) => new(true, value, []);

    public static CommandResult<T> Failure(IEnumerable<ResultError> errors) => new(false, default, errors.ToList());

    public static CommandResult<T> Failure(ResultError error) => new(false, default, [error]);
}
