using backend.Shared.Application.Results;

namespace backend.Shared.Application.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string message) 
        : base(message)
    {
        StatusCode = 400;
        Title = "Validation failed";
    }

    public ValidationException(IEnumerable<ResultError> errors)
        : base(BuildDetails(errors))
    {
        Errors = errors.ToList();
        StatusCode = 400;
        Title = "Validation failed";
    }

    public int StatusCode { get; }
    public string Title { get; }
    public List<ResultError> Errors { get; } = [];

    private static string BuildDetails(IEnumerable<ResultError> errors)
    {
        return string.Join("; ", errors.Select(e => e.Message));
    }
}
