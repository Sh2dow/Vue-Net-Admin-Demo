using backend.Domain.Models;
using backend.Shared.Application.Exceptions;
using backend.Shared.Application.Users;

namespace backend.Infrastructure.Application.Users;

public sealed class EffectiveUserAccessor : IEffectiveUserAccessor
{
    private readonly IUserDirectory _userDirectory;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public EffectiveUserAccessor(
        IUserDirectory userDirectory,
        ICurrentUserAccessor currentUser,
        IHttpContextAccessor httpContextAccessor)
    {
        _userDirectory = userDirectory;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Guid> GetUserIdAsync(CancellationToken ct = default)
    {
        var user = await GetUserAsync(ct);
        return user.Id;
    }

    public async Task<AppUser> GetUserAsync(CancellationToken ct = default)
    {
        var sub = _currentUser.Subject;
        var rawAsUserId = _httpContextAccessor.HttpContext?.Request.Query["asUserId"].FirstOrDefault();
        var isAuthenticated = !string.IsNullOrWhiteSpace(sub);
        var hasAsUserId = !string.IsNullOrWhiteSpace(rawAsUserId);
        var roles = _httpContextAccessor.HttpContext?.User.Claims
            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList() ?? new List<string>();

        // Impersonation diagnostics: log whenever asUserId is present so we can trace mismatches in production
        if (hasAsUserId)
        {
            // ReSharper disable once TemplateIsNotCompileTimeConstantProblem
            System.Diagnostics.Debug.WriteLine(
                $"[EffectiveUserAccessor] sub={sub}, isAuthenticated={isAuthenticated}, rawAsUserId={rawAsUserId}, roles=[{string.Join(",", roles)}]");
        }

        if (!isAuthenticated)
        {
            if (hasAsUserId)
            {
                // This usually means the JWT token was rejected or missing downstream.
                // Throwing here makes the failure explicit instead of silently returning anonymous data.
                throw new HttpProblemException(
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Impersonation requires a valid JWT token.");
            }

            return await _userDirectory.EnsureAsync("anonymous", "Anonymous", null, ct);
        }

        var currentUser = await _userDirectory.EnsureAsync(sub, _currentUser.PreferredUsername, _currentUser.Email, ct);

        if (!hasAsUserId)
        {
            return currentUser;
        }

        if (!Guid.TryParse(rawAsUserId, out var asUserId))
        {
            throw new HttpProblemException(
                StatusCodes.Status400BadRequest,
                "Invalid asUserId",
                "The asUserId query parameter must be a GUID.");
        }

        if (asUserId == currentUser.Id)
        {
            return currentUser;
        }

        if (!_currentUser.IsInRole("admin"))
        {
            throw new HttpProblemException(StatusCodes.Status403Forbidden, "Forbidden", "Admin role is required for impersonation.");
        }

        var targetUser = await _userDirectory.FindByIdAsync(asUserId, ct);

        if (targetUser == null)
        {
            throw new HttpProblemException(StatusCodes.Status404NotFound, "Not Found", "Target user not found.");
        }

        return targetUser;
    }
}
