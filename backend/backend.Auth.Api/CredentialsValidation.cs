using backend.Domain.Data;
using backend.Domain.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace backend.Auth.Api;

/// <summary>
/// Validates username/password credentials against the local user store.
/// </summary>
public static class CredentialsValidation
{
    public static async Task<AppUser?> HandleAsync(
        HttpContext context,
        CancellationToken cancellationToken = default)
    {
        var username = context.Request.Form["username"];
        var password = context.Request.Form["password"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return null;
        }

        var dbContext = context.RequestServices.GetRequiredService<AuthDbContext>();
        var user = await dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (user is null || user.PasswordHash is null)
        {
            return null;
        }

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            return null;
        }

        return user;
    }
}
