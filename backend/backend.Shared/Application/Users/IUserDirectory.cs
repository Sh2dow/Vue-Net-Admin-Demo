using backend.Domain.Models;

namespace backend.Shared.Application.Users;

public interface IUserDirectory
{
    Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, AppUser>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
    Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> FindBySubjectAsync(string subject, CancellationToken ct = default);
    Task<AppUser> EnsureAsync(string subject, string? preferredUsername, string? email, CancellationToken ct = default);
    Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default);
    Task<AppUser> UpdateAsync(AppUser user, CancellationToken ct = default);
    Task<int> DeleteByIdAsync(Guid id, CancellationToken ct = default);
}
