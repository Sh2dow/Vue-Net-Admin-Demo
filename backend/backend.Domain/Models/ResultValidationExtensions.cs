namespace backend.Domain.Models;

public static class ResultValidationExtensions
{
    public static bool IsValid<T>(this Result<T> result)
        => result.IsSuccess;

    public static bool IsValid<T>(this CommandResult<T> result)
        => result.IsSuccess;

    public static bool IsValid<T>(this DomainResult<T> result)
        => result.IsSuccess;

    public static T GetValueOrThrow<T>(this Result<T> result, string? message = null)
        => result.IsSuccess ? result.Value! : throw new InvalidOperationException(message ?? "Result is invalid.");

    public static T GetValueOrThrow<T>(this CommandResult<T> result, string? message = null)
        => result.IsSuccess ? result.Value! : throw new InvalidOperationException(message ?? "Result is invalid.");

    public static T GetValueOrThrow<T>(this DomainResult<T> result, string? message = null)
        => result.IsSuccess ? result.Value! : throw new InvalidOperationException(message ?? "Result is invalid.");

    public static CommandResult<T> ToCommandResult<T>(this Result<T> result)
        => result.IsSuccess
            ? CommandResult<T>.Success(result.Value!)
            : CommandResult<T>.Failure(result.Errors);

    public static DomainResult<T> ToDomainResult<T>(this Result<T> result)
        => result.IsSuccess
            ? DomainResult<T>.Success(result.Value!)
            : DomainResult<T>.Failure(result.Errors);

    public static Result<T> ToResult<T>(this CommandResult<T> commandResult)
        => commandResult.IsSuccess
            ? Result<T>.Success(commandResult.Value!)
            : Result<T>.Validation(commandResult.Errors);

    public static Result<T> ToResult<T>(this DomainResult<T> domainResult)
        => domainResult.IsSuccess
            ? Result<T>.Success(domainResult.Value!)
            : Result<T>.Validation(domainResult.Errors);
}
