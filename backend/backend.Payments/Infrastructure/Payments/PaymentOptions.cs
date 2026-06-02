namespace backend.Payments.Infrastructure.Payments;

public sealed class PaymentOptions
{
    public const string SectionName = "Payments";

    public bool AutoAuthorize { get; set; } = true;
    public int StubDelayMilliseconds { get; set; } = 250;
}
