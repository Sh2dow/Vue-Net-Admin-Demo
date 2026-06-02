using System.Reflection;
using backend.Shared.Application.Abstractions;
using backend.Shared.Application.Results;
using FluentValidation;
using MediatR;

namespace backend.Infrastructure.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private static readonly Type CommandInterfaceType = typeof(ICommand<>);
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any() || !IsCommandRequest()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                failures.AddRange(result.Errors);
            }
        }

        if (failures.Count == 0) return await next();

        return BuildValidationResult(failures);
    }

    private static bool IsCommandRequest() =>
        typeof(TRequest)
            .GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == CommandInterfaceType);

    private static TResponse BuildValidationResult(
        IReadOnlyList<FluentValidation.Results.ValidationFailure> failures)
    {
        if (!typeof(TResponse).IsGenericType || typeof(TResponse).GetGenericTypeDefinition() != typeof(Result<>))
        {
            throw new InvalidOperationException(
                $"Validation behavior requires commands to return Result<T>. Request={typeof(TRequest).Name}, Response={typeof(TResponse).Name}");
        }

        var errors = failures
            .Select(x => new ResultError("validation", x.ErrorMessage, x.PropertyName))
            .ToList();

        var validationMethod = typeof(TResponse).GetMethod(
            "Validation",
            BindingFlags.Public | BindingFlags.Static,
            [typeof(IEnumerable<ResultError>)]);

        if (validationMethod == null)
        {
            throw new InvalidOperationException($"Validation factory was not found on {typeof(TResponse).Name}.");
        }

        var response = validationMethod.Invoke(null, [errors]);
        return (TResponse)response!;
    }
}
