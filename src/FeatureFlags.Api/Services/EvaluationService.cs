using FeatureFlags.Api.Data;
using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeatureFlags.Api.Services;

public class EvaluationService
{
    private readonly AppDbContext _db;

    public EvaluationService(AppDbContext db) => _db = db;

    public async Task<EvaluateResponse?> EvaluateFlagAsync(string environmentName, string flagKey, EvaluateRequest request)
    {
        var fev = await _db.FlagEnvironmentValues
            .Include(f => f.Flag)
            .Include(f => f.Environment)
            .Include(f => f.Rules.OrderBy(r => r.Order))
            .AsNoTracking()
            .FirstOrDefaultAsync(f =>
                f.Flag.Key == flagKey &&
                !f.Flag.IsArchived &&
                f.Environment.Name == environmentName);

        if (fev == null)
            return new EvaluateResponse(flagKey, false, null, null);

        if (!fev.IsEnabled)
            return new EvaluateResponse(flagKey, false, ParseValue(fev.Value, fev.Flag.Type), null);

        // Evaluate rules in order
        foreach (var rule in fev.Rules)
        {
            var (matched, serveValue) = EvaluateRule(rule, request, flagKey);
            if (matched)
            {
                var matchedValue = serveValue != null ? ParseValue(serveValue, fev.Flag.Type) : ParseValue(fev.Value, fev.Flag.Type);
                return new EvaluateResponse(flagKey, true, matchedValue, $"rule:{rule.Id}:{rule.Type}");
            }
        }

        return new EvaluateResponse(flagKey, true, ParseValue(fev.Value, fev.Flag.Type), null);
    }

    private (bool Matched, string? ServeValue) EvaluateRule(TargetingRule rule, EvaluateRequest request, string flagKey)
    {
        if (rule.Type == RuleType.Percentage)
        {
            var bucket = GetStableBucket(flagKey, request.UserId);
            return (bucket < rule.Percentage, rule.ServeValue);
        }

        if (rule.Type == RuleType.Attribute && rule.AttributeKey != null)
        {
            var attrs = request.Attributes ?? new Dictionary<string, string>();
            if (!attrs.TryGetValue(rule.AttributeKey, out var attrVal))
                return (false, null);

            var matched = rule.Operator switch
            {
                RuleOperator.Equals => string.Equals(attrVal, rule.AttributeValue, StringComparison.OrdinalIgnoreCase),
                RuleOperator.NotEquals => !string.Equals(attrVal, rule.AttributeValue, StringComparison.OrdinalIgnoreCase),
                RuleOperator.InList => IsInList(attrVal, rule.AttributeValue),
                _ => false
            };
            return (matched, matched ? rule.ServeValue : null);
        }

        return (false, null);
    }

    // Stable deterministic bucket: SHA256(flagKey + ":" + userId) → 0..99
    public static int GetStableBucket(string flagKey, string userId)
    {
        var input = $"{flagKey}:{userId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var num = BitConverter.ToUInt32(hash, 0);
        return (int)(num % 100);
    }

    private static bool IsInList(string value, string? listJson)
    {
        if (listJson == null) return false;
        try
        {
            var list = JsonSerializer.Deserialize<string[]>(listJson);
            return list?.Any(v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase)) == true;
        }
        catch { return false; }
    }

    private static object? ParseValue(string? val, FlagType type)
    {
        if (val == null) return null;
        return type switch
        {
            FlagType.Boolean => val.Trim() == "true",
            FlagType.Number => double.TryParse(val, out var d) ? (object)d : val,
            _ => val.Trim('"')
        };
    }

    public async Task<(IEnumerable<SnapshotFlagDto> Flags, string ETag)> GetSnapshotAsync(string environmentName)
    {
        var values = await _db.FlagEnvironmentValues
            .Include(f => f.Flag)
            .Include(f => f.Environment)
            .Include(f => f.Rules.OrderBy(r => r.Order))
            .AsNoTracking()
            .Where(f => f.Environment.Name == environmentName && !f.Flag.IsArchived)
            .ToListAsync();

        var snapshots = values.Select(fev => new SnapshotFlagDto(
            fev.Flag.Key,
            fev.IsEnabled,
            ParseValue(fev.Value, fev.Flag.Type)
        )).OrderBy(s => s.Key).ToList();

        var json = JsonSerializer.Serialize(snapshots);
        var etagBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var etag = $"\"{Convert.ToHexString(etagBytes).ToLowerInvariant()}\"";

        return (snapshots, etag);
    }
}
