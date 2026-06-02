using backend.Domain.Models;

namespace backend.Shared.Application.Users;

public interface IEffectiveUserAccessor
{
    Task<Guid> GetUserIdAsync(CancellationToken ct = default);
    Task<AppUser> GetUserAsync(CancellationToken ct = default);
}
