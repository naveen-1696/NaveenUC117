namespace FeatureFlags.Api.DTOs;

public record EvaluateRequest(
    string UserId,
    Dictionary<string, string>? Attributes
);

public record EvaluateResponse(
    string FlagKey,
    bool IsEnabled,
    object? Value,
    string? RuleMatched
);

public record SnapshotFlagDto(
    string Key,
    bool IsEnabled,
    object? Value
);
