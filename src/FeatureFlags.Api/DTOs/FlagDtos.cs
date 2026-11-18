using FeatureFlags.Api.Models;

namespace FeatureFlags.Api.DTOs;

public record CreateFlagRequest(
    string Key,
    string Name,
    string? Description,
    FlagType Type
);

public record UpdateFlagRequest(
    string Name,
    string? Description,
    string RowVersion
);

public record FlagDto(
    int Id,
    string Key,
    string Name,
    string? Description,
    FlagType Type,
    bool IsArchived,
    DateTime CreatedAt,
    string CreatedByUserId,
    string RowVersion
);

public record FlagListResponse(
    IEnumerable<FlagDto> Items,
    int TotalCount,
    int Page,
    int PageSize
);

public record EnvironmentDto(int Id, string Name, bool IsProtected);

public record FlagEnvironmentValueDto(
    int Id,
    int FlagId,
    int EnvironmentId,
    string EnvironmentName,
    bool IsEnabled,
    string? Value,
    string RowVersion,
    IEnumerable<TargetingRuleDto> Rules
);

public record TargetingRuleDto(
    int Id,
    RuleType Type,
    int? Percentage,
    string? AttributeKey,
    RuleOperator? Operator,
    string? AttributeValue,
    int Order,
    string? ServeValue
);

public record UpdateFlagEnvironmentRequest(
    bool IsEnabled,
    string? Value,
    IEnumerable<TargetingRuleRequest> Rules,
    string Reason,
    string RowVersion
);

public record TargetingRuleRequest(
    RuleType Type,
    int? Percentage,
    string? AttributeKey,
    RuleOperator? Operator,
    string? AttributeValue,
    int Order,
    string? ServeValue
);

public record FlagVersionDto(
    int Id,
    int FlagId,
    int EnvironmentId,
    int Version,
    string ChangedByUserId,
    DateTime ChangedAt,
    string Reason,
    string Snapshot,
    bool IsRollback
);

public record ChangeRequestDto(
    int Id,
    int FlagId,
    string FlagKey,
    string FlagName,
    int EnvironmentId,
    string EnvironmentName,
    string RequestedByUserId,
    string? ReviewedByUserId,
    ChangeRequestStatus Status,
    string ProposedChanges,
    string Reason,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? ReviewNote
);

public record ReviewChangeRequest(string? ReviewNote);

public record ApiKeyDto(int Id, int EnvironmentId, string EnvironmentName, string Name, bool IsActive, DateTime CreatedAt);

public record CreateApiKeyRequest(int EnvironmentId, string Name);

public record CreateApiKeyResponse(int Id, string PlainKey, string Name, int EnvironmentId);

public record ErrorResponse(string Error, string? Detail = null);
