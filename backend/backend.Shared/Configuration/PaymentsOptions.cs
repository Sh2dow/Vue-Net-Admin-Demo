namespace backend.Shared.Configuration;

/// <summary>
/// Configuration options for payments service.
/// </summary>
public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Enables automatic authorization for payments in development.
    /// </summary>
    public bool AutoAuthorize { get; init; } = true;

    /// <summary>
    /// Delay in milliseconds for payment processing stub.
    /// </summary>
    public int StubDelayMilliseconds { get; init; } = 250;
}
