namespace FeatureFlags.Api.Models;

public class FlagVersion
{
    public int Id { get; set; }
    public int FlagId { get; set; }
    public int EnvironmentId { get; set; }
    public int Version { get; set; }
    public string ChangedByUserId { get; set; } = default!;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = default!;
    public string Snapshot { get; set; } = default!;
    public bool IsRollback { get; set; }

    public Flag Flag { get; set; } = default!;
    public AppEnvironment Environment { get; set; } = default!;
}
