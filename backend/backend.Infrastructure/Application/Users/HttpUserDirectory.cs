using System.Net;
using System.Net.Http.Json;
using backend.Domain.Models;
using backend.Shared.Application.Users;

namespace backend.Infrastructure.Application.Users;

public sealed class HttpUserDirectory : IUserDirectory
{
    private readonly HttpClient _httpClient;

    public HttpUserDirectory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<AppUser>> ListAsync(CancellationToken ct = default)
    {
        var users = await _httpClient.GetFromJsonAsync<List<AuthUserDto>>("internal/users", ct)
            ?? [];

        return users.Select(ToEntity).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, AppUser>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var distinctIds = ids.Distinct().ToArray();
        if (distinctIds.Length == 0)
        {
            return new Dictionary<Guid, AppUser>();
        }

        var query = string.Join("&", distinctIds.Select(id => $"ids={Uri.EscapeDataString(id.ToString())}"));
        var users = await _httpClient.GetFromJsonAsync<List<AuthUserDto>>($"internal/users/batch?{query}", ct)
            ?? [];

        return users.Select(ToEntity).ToDictionary(x => x.Id);
    }

    public async Task<AppUser?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync($"internal/users/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken: ct);
        return dto is null ? null : ToEntity(dto);
    }

    public async Task<AppUser?> FindBySubjectAsync(string subject, CancellationToken ct = default)
    {
        using var response = await _httpClient.GetAsync(
            $"internal/users/by-subject/{Uri.EscapeDataString(subject)}",
            ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken: ct);
        return dto is null ? null : ToEntity(dto);
    }

    public async Task<AppUser> EnsureAsync(string subject, string? preferredUsername, string? email, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "internal/users/ensure",
            new EnsureAuthUserRequest(subject, preferredUsername, email),
            ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Auth service returned an empty ensure response.");

        return ToEntity(dto);
    }

    public async Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "internal/users",
            new CreateAuthUserRequest(user.Subject, user.Username, user.Email),
            ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Auth service returned an empty create response.");

        return ToEntity(dto);
    }

    public async Task<AppUser> UpdateAsync(AppUser user, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"internal/users/{user.Id}",
            new UpdateAuthUserRequest(user.Username, user.Email),
            ct);
        response.EnsureSuccessStatusCode();

        var dto = await response.Content.ReadFromJsonAsync<AuthUserDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Auth service returned an empty update response.");

        return ToEntity(dto);
    }

    public async Task<int> DeleteByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var response = await _httpClient.DeleteAsync($"internal/users/{id}", ct);
        return response.StatusCode switch
        {
            HttpStatusCode.NoContent => 1,
            HttpStatusCode.NotFound => 0,
            _ => throw new HttpRequestException(
                $"Unexpected auth service response: {(int)response.StatusCode} {response.ReasonPhrase}")
        };
    }

    private static AppUser ToEntity(AuthUserDto dto) =>
        new()
        {
            Id = dto.Id,
            Subject = dto.Subject,
            Username = dto.Username,
            Email = dto.Email,
            CreatedAtUtc = dto.CreatedAtUtc
        };
}
