namespace backend.Shared.Configuration;

/// <summary>
/// Configuration options for database connections.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";

    /// <summary>
    /// Default database connection string.
    /// </summary>
    /// <remarks>Should be set via environment variable in production.</remarks>
    public string Default { get; init; } = string.Empty;

    /// <summary>
    /// Auth database connection string.
    /// </summary>
    /// <remarks>Should be set via environment variable in production.</remarks>
    public string Auth { get; init; } = string.Empty;
}
