namespace backend.Api;

public sealed class DownstreamServicesOptions
{
    public const string SectionName = "DownstreamServices";

    public string UsersBaseUrl { get; set; } = "http://localhost:5005";

    public string TasksBaseUrl { get; set; } = "http://localhost:5002";

    public string OrdersBaseUrl { get; set; } = "http://localhost:5003";

    public string PaymentsBaseUrl { get; set; } = "http://localhost:5004";
}
