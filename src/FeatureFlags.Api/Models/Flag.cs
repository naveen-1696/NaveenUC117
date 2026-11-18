using System.ComponentModel.DataAnnotations;

namespace FeatureFlags.Api.Models;

public class Flag
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = default!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = default!;

    [MaxLength(500)]
    public string? Description { get; set; }

    public FlagType Type { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = default!;

    [ConcurrencyCheck]
    public string RowVersion { get; set; } = Guid.NewGuid().ToString("N");

    public ICollection<FlagEnvironmentValue> EnvironmentValues { get; set; } = new List<FlagEnvironmentValue>();
    public ICollection<FlagVersion> Versions { get; set; } = new List<FlagVersion>();
    public ICollection<ChangeRequest> ChangeRequests { get; set; } = new List<ChangeRequest>();
}
