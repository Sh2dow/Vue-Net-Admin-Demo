namespace backend.Shared.Application.Exceptions;

public class IntegrationException : Exception
{
    public IntegrationException(string message) 
        : base(message)
    {
        StatusCode = 500;
        Title = "Integration failed";
    }

    public IntegrationException(string message, Exception innerException) 
        : base(message, innerException)
    {
        StatusCode = 500;
        Title = "Integration failed";
        InnerException = innerException;
    }

    public int StatusCode { get; }
    public string Title { get; }
    public new Exception? InnerException { get; }
}
