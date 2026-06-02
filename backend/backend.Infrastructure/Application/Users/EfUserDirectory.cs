using backend.Domain.Data;
using backend.Domain.Models;
using backend.Shared.Application.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Infrastructure.Application.Users;

public sealed class EfUserDirectory : IUserDirectory
{
    private readonly AuthDbContext _db;

    public EfUserDirectory(AuthDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, AppUser>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, AppUser>();
        }

        return await _db.AppUsers
            .AsNoTracking()
            .Where(x => distinctIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
    }

    public Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public Task<AppUser?> FindBySubjectAsync(string subject, CancellationToken ct = default)
    {
        return _db.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Subject == subject, ct);
    }

    public async Task<AppUser> EnsureAsync(string subject, string? preferredUsername, string? email, CancellationToken ct = default)
    {
        var fallbackUserNameSuffix = subject.Length <= 8 ? subject : subject[..8];
        var user = await _db.AppUsers.FirstOrDefaultAsync(x => x.Subject == subject, ct);
        if (user != null)
        {
            return user;
        }

        user = new AppUser
        {
            Subject = subject,
            Username = preferredUsername ?? $"user-{fallbackUserNameSuffix}",
            Email = email
        };

        _db.AppUsers.Add(user);

        try
        {
            await _db.SaveChangesAsync(ct);
            return user;
        }
        catch (DbUpdateException ex) when (IsDuplicateSubjectViolation(ex))
        {
            _db.Entry(user).State = EntityState.Detached;

            return await _db.AppUsers.FirstAsync(x => x.Subject == subject, ct);
        }
    }

    public async Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default)
    {
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }

    public Task<int> DeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        return _db.AppUsers
            .Where(x => x.Id == id)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<AppUser> UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
        return user;
    }

    private static bool IsDuplicateSubjectViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
        && string.Equals(postgresException.ConstraintName, "ix_app_users_subject", StringComparison.OrdinalIgnoreCase);
}
