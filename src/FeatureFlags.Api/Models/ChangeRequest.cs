namespace FeatureFlags.Api.Models;

public class ChangeRequest
{
    public int Id { get; set; }
    public int FlagId { get; set; }
    public int EnvironmentId { get; set; }
    public string RequestedByUserId { get; set; } = default!;
    public string? ReviewedByUserId { get; set; }
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;
    public string ProposedChanges { get; set; } = default!;
    public string Reason { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }

    public Flag Flag { get; set; } = default!;
    public AppEnvironment Environment { get; set; } = default!;
}
