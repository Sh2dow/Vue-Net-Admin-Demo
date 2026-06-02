namespace backend.Domain.Models;

public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Subject { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>
    /// BCrypt password hash. Null when the user is an external/identity-only account.
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Comma-separated role names (e.g. "admin,user"). Used for simple role-based authorization.
    /// </summary>
    public string Roles { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
