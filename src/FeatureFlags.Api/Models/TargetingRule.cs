namespace FeatureFlags.Api.Models;

public class TargetingRule
{
    public int Id { get; set; }
    public int FlagEnvironmentValueId { get; set; }
    public RuleType Type { get; set; }
    public int? Percentage { get; set; }
    public string? AttributeKey { get; set; }
    public RuleOperator? Operator { get; set; }
    public string? AttributeValue { get; set; }
    public int Order { get; set; }
    public string? ServeValue { get; set; }

    public FlagEnvironmentValue FlagEnvironmentValue { get; set; } = default!;
}
