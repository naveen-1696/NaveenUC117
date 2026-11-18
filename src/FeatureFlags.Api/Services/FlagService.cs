using FeatureFlags.Api.Data;
using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeatureFlags.Api.Services;

public class FlagService
{
    private readonly AppDbContext _db;

    public FlagService(AppDbContext db) => _db = db;

    public async Task<FlagListResponse> GetFlagsAsync(int page, int pageSize, string? search)
    {
        var query = _db.Flags.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.Key.Contains(search) || f.Name.Contains(search));

        var total = await query.CountAsync();
        var flags = await query
            .OrderBy(f => f.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new FlagListResponse(flags.Select(MapFlag), total, page, pageSize);
    }

    public async Task<FlagDto?> GetFlagAsync(int id)
    {
        var flag = await _db.Flags.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        return flag == null ? null : MapFlag(flag);
    }

    public async Task<(FlagDto? Flag, string? Error)> CreateFlagAsync(CreateFlagRequest req, string userId)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(req.Key, @"^[a-z0-9\-]+$"))
            return (null, "Flag key must be lowercase alphanumeric with hyphens only.");

        if (await _db.Flags.AnyAsync(f => f.Key == req.Key))
            return (null, "Flag key already exists.");

        var flag = new Flag
        {
            Key = req.Key,
            Name = req.Name,
            Description = req.Description,
            Type = req.Type,
            CreatedByUserId = userId
        };
        _db.Flags.Add(flag);

        // Create default FlagEnvironmentValue for each environment
        var envs = await _db.Environments.ToListAsync();
        foreach (var env in envs)
        {
            _db.FlagEnvironmentValues.Add(new FlagEnvironmentValue
            {
                FlagId = flag.Id,
                EnvironmentId = env.Id,
                IsEnabled = false,
                Value = req.Type == FlagType.Boolean ? "false" : null
            });
        }

        await _db.SaveChangesAsync();

        // Record version 1 for each environment
        var defaultVal = req.Type == FlagType.Boolean ? "false" : null;
        foreach (var env in envs)
        {
            var fev = await _db.FlagEnvironmentValues.FirstAsync(f => f.FlagId == flag.Id && f.EnvironmentId == env.Id);
            _db.FlagVersions.Add(new FlagVersion
            {
                FlagId = flag.Id,
                EnvironmentId = env.Id,
                Version = 1,
                ChangedByUserId = userId,
                Reason = "Flag created",
                Snapshot = JsonSerializer.Serialize(new { IsEnabled = false, Value = defaultVal, Rules = Array.Empty<object>() })
            });
        }
        await _db.SaveChangesAsync();

        return (MapFlag(flag), null);
    }

    public async Task<(FlagDto? Flag, string? Error)> UpdateFlagAsync(int id, UpdateFlagRequest req, string userId)
    {
        var flag = await _db.Flags.FirstOrDefaultAsync(f => f.Id == id);
        if (flag == null) return (null, "Not found");

        if (flag.RowVersion != req.RowVersion)
            return (null, "CONFLICT");

        flag.Name = req.Name;
        flag.Description = req.Description;
        await _db.SaveChangesAsync();
        return (MapFlag(flag), null);
    }

    public async Task<string?> ArchiveFlagAsync(int id)
    {
        var flag = await _db.Flags.FirstOrDefaultAsync(f => f.Id == id);
        if (flag == null) return "Not found";
        flag.IsArchived = true;
        await _db.SaveChangesAsync();
        return null;
    }

    public async Task<(FlagEnvironmentValueDto? Value, string? Error)> GetFlagEnvironmentValueAsync(int flagId, int envId)
    {
        var fev = await _db.FlagEnvironmentValues
            .Include(f => f.Flag)
            .Include(f => f.Environment)
            .Include(f => f.Rules.OrderBy(r => r.Order))
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FlagId == flagId && f.EnvironmentId == envId);

        return fev == null ? (null, "Not found") : (MapFev(fev), null);
    }

    public async Task<(object? Result, string? Error)> UpdateFlagEnvironmentAsync(
        int flagId, int envId, UpdateFlagEnvironmentRequest req, string userId, bool isAdmin)
    {
        var env = await _db.Environments.FindAsync(envId);
        if (env == null) return (null, "Environment not found");

        var fev = await _db.FlagEnvironmentValues
            .Include(f => f.Rules)
            .FirstOrDefaultAsync(f => f.FlagId == flagId && f.EnvironmentId == envId);
        if (fev == null) return (null, "Flag environment not found");

        if (fev.RowVersion != req.RowVersion)
            return (null, "CONFLICT");

        // Protected environments require approval (unless admin doing a rollback)
        if (env.IsProtected && !isAdmin)
        {
            var changeRequest = new ChangeRequest
            {
                FlagId = flagId,
                EnvironmentId = envId,
                RequestedByUserId = userId,
                Reason = req.Reason,
                ProposedChanges = JsonSerializer.Serialize(new { req.IsEnabled, req.Value, req.Rules })
            };
            _db.ChangeRequests.Add(changeRequest);
            await _db.SaveChangesAsync();
            return (new { ChangeRequestId = changeRequest.Id, Message = "Change request submitted for approval" }, null);
        }

        await ApplyFevChangeAsync(fev, req, userId, flagId, envId, false);
        return (MapFev(fev), null);
    }

    public async Task<(object? Result, string? Error)> ApproveChangeRequestAsync(int changeRequestId, string reviewerId, string? reviewNote)
    {
        var cr = await _db.ChangeRequests.Include(c => c.Flag).FirstOrDefaultAsync(c => c.Id == changeRequestId);
        if (cr == null) return (null, "Not found");
        if (cr.Status != ChangeRequestStatus.Pending) return (null, "Already reviewed");
        if (cr.RequestedByUserId == reviewerId) return (null, "Cannot approve your own change");

        var fev = await _db.FlagEnvironmentValues
            .Include(f => f.Rules)
            .FirstOrDefaultAsync(f => f.FlagId == cr.FlagId && f.EnvironmentId == cr.EnvironmentId);
        if (fev == null) return (null, "Flag environment not found");

        var proposed = JsonSerializer.Deserialize<ProposedChangeData>(cr.ProposedChanges,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var req = new UpdateFlagEnvironmentRequest(
            proposed.IsEnabled,
            proposed.Value,
            proposed.Rules ?? Enumerable.Empty<TargetingRuleRequest>(),
            cr.Reason,
            fev.RowVersion
        );

        cr.Status = ChangeRequestStatus.Approved;
        cr.ReviewedByUserId = reviewerId;
        cr.ReviewedAt = DateTime.UtcNow;
        cr.ReviewNote = reviewNote;

        await ApplyFevChangeAsync(fev, req, reviewerId, cr.FlagId, cr.EnvironmentId, false);
        return (new { Message = "Approved and applied" }, null);
    }

    public async Task<(object? Result, string? Error)> RejectChangeRequestAsync(int changeRequestId, string reviewerId, string? reviewNote)
    {
        var cr = await _db.ChangeRequests.FirstOrDefaultAsync(c => c.Id == changeRequestId);
        if (cr == null) return (null, "Not found");
        if (cr.Status != ChangeRequestStatus.Pending) return (null, "Already reviewed");
        if (cr.RequestedByUserId == reviewerId) return (null, "Cannot reject your own change");

        cr.Status = ChangeRequestStatus.Rejected;
        cr.ReviewedByUserId = reviewerId;
        cr.ReviewedAt = DateTime.UtcNow;
        cr.ReviewNote = reviewNote;

        await _db.SaveChangesAsync();
        return (new { Message = "Rejected" }, null);
    }

    public async Task<(FlagEnvironmentValueDto? Result, string? Error)> RollbackAsync(int flagId, int envId, int versionId, string userId)
    {
        var version = await _db.FlagVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.FlagId == flagId && v.EnvironmentId == envId);
        if (version == null) return (null, "Version not found");

        var fev = await _db.FlagEnvironmentValues
            .Include(f => f.Rules)
            .FirstOrDefaultAsync(f => f.FlagId == flagId && f.EnvironmentId == envId);
        if (fev == null) return (null, "Flag environment not found");

        var snapshot = JsonSerializer.Deserialize<SnapshotData>(version.Snapshot,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var req = new UpdateFlagEnvironmentRequest(
            snapshot.IsEnabled,
            snapshot.Value,
            snapshot.Rules?.Select(r => new TargetingRuleRequest(
                r.Type, r.Percentage, r.AttributeKey, r.Operator, r.AttributeValue, r.Order, r.ServeValue
            )) ?? Enumerable.Empty<TargetingRuleRequest>(),
            $"Rollback to version {version.Version}",
            fev.RowVersion
        );

        await ApplyFevChangeAsync(fev, req, userId, flagId, envId, true);
        return (MapFev(fev), null);
    }

    private async Task ApplyFevChangeAsync(FlagEnvironmentValue fev, UpdateFlagEnvironmentRequest req,
        string userId, int flagId, int envId, bool isRollback)
    {
        // Get next version number
        var nextVersion = (await _db.FlagVersions
            .Where(v => v.FlagId == flagId && v.EnvironmentId == envId)
            .MaxAsync(v => (int?)v.Version) ?? 0) + 1;

        // Snapshot before applying
        var snapshot = JsonSerializer.Serialize(new SnapshotData
        {
            IsEnabled = req.IsEnabled,
            Value = req.Value,
            Rules = req.Rules.Select(r => new SnapshotRule
            {
                Type = r.Type, Percentage = r.Percentage, AttributeKey = r.AttributeKey,
                Operator = r.Operator, AttributeValue = r.AttributeValue, Order = r.Order, ServeValue = r.ServeValue
            }).ToList()
        });

        // Apply changes to FEV
        fev.IsEnabled = req.IsEnabled;
        fev.Value = req.Value;

        // Replace rules
        _db.TargetingRules.RemoveRange(fev.Rules);
        fev.Rules.Clear();
        int order = 1;
        foreach (var ruleReq in req.Rules.OrderBy(r => r.Order))
        {
            fev.Rules.Add(new TargetingRule
            {
                FlagEnvironmentValueId = fev.Id,
                Type = ruleReq.Type,
                Percentage = ruleReq.Percentage,
                AttributeKey = ruleReq.AttributeKey,
                Operator = ruleReq.Operator,
                AttributeValue = ruleReq.AttributeValue,
                Order = order++,
                ServeValue = ruleReq.ServeValue
            });
        }

        // Add version record
        _db.FlagVersions.Add(new FlagVersion
        {
            FlagId = flagId,
            EnvironmentId = envId,
            Version = nextVersion,
            ChangedByUserId = userId,
            Reason = req.Reason,
            Snapshot = snapshot,
            IsRollback = isRollback
        });

        await _db.SaveChangesAsync();
    }

    public async Task<IEnumerable<FlagVersionDto>> GetHistoryAsync(int flagId, int envId)
    {
        return await _db.FlagVersions
            .AsNoTracking()
            .Where(v => v.FlagId == flagId && v.EnvironmentId == envId)
            .OrderByDescending(v => v.Version)
            .Select(v => new FlagVersionDto(v.Id, v.FlagId, v.EnvironmentId, v.Version,
                v.ChangedByUserId, v.ChangedAt, v.Reason, v.Snapshot, v.IsRollback))
            .ToListAsync();
    }

    public async Task<IEnumerable<ChangeRequestDto>> GetPendingChangeRequestsAsync()
    {
        return await _db.ChangeRequests
            .Include(cr => cr.Flag)
            .Include(cr => cr.Environment)
            .AsNoTracking()
            .Where(cr => cr.Status == ChangeRequestStatus.Pending)
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(cr => MapCr(cr))
            .ToListAsync();
    }

    public async Task<IEnumerable<ChangeRequestDto>> GetAllChangeRequestsAsync()
    {
        return await _db.ChangeRequests
            .Include(cr => cr.Flag)
            .Include(cr => cr.Environment)
            .AsNoTracking()
            .OrderByDescending(cr => cr.CreatedAt)
            .Select(cr => MapCr(cr))
            .ToListAsync();
    }

    private static FlagDto MapFlag(Flag f) => new(
        f.Id, f.Key, f.Name, f.Description, f.Type, f.IsArchived, f.CreatedAt, f.CreatedByUserId,
        f.RowVersion);

    private static FlagEnvironmentValueDto MapFev(FlagEnvironmentValue fev) => new(
        fev.Id, fev.FlagId, fev.EnvironmentId,
        fev.Environment?.Name ?? "",
        fev.IsEnabled, fev.Value,
        fev.RowVersion,
        fev.Rules.OrderBy(r => r.Order).Select(r => new TargetingRuleDto(
            r.Id, r.Type, r.Percentage, r.AttributeKey, r.Operator, r.AttributeValue, r.Order, r.ServeValue)));

    private static ChangeRequestDto MapCr(ChangeRequest cr) => new(
        cr.Id, cr.FlagId, cr.Flag?.Key ?? "", cr.Flag?.Name ?? "",
        cr.EnvironmentId, cr.Environment?.Name ?? "",
        cr.RequestedByUserId, cr.ReviewedByUserId, cr.Status,
        cr.ProposedChanges, cr.Reason, cr.CreatedAt, cr.ReviewedAt, cr.ReviewNote);

    private record ProposedChangeData(bool IsEnabled, string? Value, IEnumerable<TargetingRuleRequest>? Rules);

    private class SnapshotData
    {
        public bool IsEnabled { get; set; }
        public string? Value { get; set; }
        public List<SnapshotRule>? Rules { get; set; }
    }

    private class SnapshotRule
    {
        public RuleType Type { get; set; }
        public int? Percentage { get; set; }
        public string? AttributeKey { get; set; }
        public RuleOperator? Operator { get; set; }
        public string? AttributeValue { get; set; }
        public int Order { get; set; }
        public string? ServeValue { get; set; }
    }
}
