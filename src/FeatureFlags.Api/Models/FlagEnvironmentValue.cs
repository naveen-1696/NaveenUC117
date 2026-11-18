using System.ComponentModel.DataAnnotations;

namespace FeatureFlags.Api.Models;

public class FlagEnvironmentValue
{
    public int Id { get; set; }
    public int FlagId { get; set; }
    public int EnvironmentId { get; set; }
    public bool IsEnabled { get; set; }
    public string? Value { get; set; }

    [ConcurrencyCheck]
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public Flag Flag { get; set; } = default!;
    public AppEnvironment Environment { get; set; } = default!;
    public ICollection<TargetingRule> Rules { get; set; } = new List<TargetingRule>();
}
