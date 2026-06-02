namespace backend.Infrastructure.Application.Users;

public sealed class AuthServiceOptions
{
    public const string SectionName = "AuthService";

    public string BaseUrl { get; set; } = "http://localhost:5001";
}
