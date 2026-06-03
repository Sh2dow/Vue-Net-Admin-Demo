using System.Security.Claims;
using backend.Shared.Application.Users;

namespace backend.Infrastructure.Application.Users;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? Subject =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? _httpContextAccessor.HttpContext?.User.Identity?.Name;

    public string? PreferredUsername =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("preferred_username");

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    public bool IsInRole(string role) =>
        _httpContextAccessor.HttpContext?.User.IsInRole(role) ?? false;
}
