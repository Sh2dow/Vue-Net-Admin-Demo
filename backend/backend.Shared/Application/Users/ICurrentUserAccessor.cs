namespace backend.Shared.Application.Users;

public interface ICurrentUserAccessor
{
    string? Subject { get; }
    string? PreferredUsername { get; }
    string? Email { get; }
    bool IsInRole(string role);
}
