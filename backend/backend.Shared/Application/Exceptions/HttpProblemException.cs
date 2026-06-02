namespace backend.Shared.Application.Exceptions;

public sealed class HttpProblemException : Exception
{
    public HttpProblemException(int statusCode, string title, string? detail = null)
        : base(detail ?? title)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }

    public int StatusCode { get; }
    public string Title { get; }
    public string? Detail { get; }
}
