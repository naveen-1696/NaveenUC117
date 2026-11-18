using FeatureFlags.Api.Data;
using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace FeatureFlags.Api.Services;

public class ApiKeyService
{
    private readonly AppDbContext _db;

    public ApiKeyService(AppDbContext db) => _db = db;

    public async Task<IEnumerable<ApiKeyDto>> GetApiKeysAsync()
    {
        return await _db.ApiKeys
            .Include(k => k.Environment)
            .AsNoTracking()
            .Select(k => new ApiKeyDto(k.Id, k.EnvironmentId, k.Environment.Name, k.Name, k.IsActive, k.CreatedAt))
            .ToListAsync();
    }

    public async Task<CreateApiKeyResponse> CreateApiKeyAsync(CreateApiKeyRequest req)
    {
        var plainKey = GeneratePlainKey();
        var hash = HashKey(plainKey);

        var key = new ApiKey
        {
            EnvironmentId = req.EnvironmentId,
            Name = req.Name,
            KeyHash = hash
        };
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync();

        return new CreateApiKeyResponse(key.Id, plainKey, key.Name, key.EnvironmentId);
    }

    public async Task<bool> RevokeAsync(int id)
    {
        var key = await _db.ApiKeys.FindAsync(id);
        if (key == null) return false;
        key.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<AppEnvironment?> ValidateApiKeyAsync(string plainKey, string environmentName)
    {
        var hash = HashKey(plainKey);
        return await _db.ApiKeys
            .Include(k => k.Environment)
            .AsNoTracking()
            .Where(k => k.KeyHash == hash && k.IsActive && k.Environment.Name == environmentName)
            .Select(k => k.Environment)
            .FirstOrDefaultAsync();
    }

    private static string GeneratePlainKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string HashKey(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
