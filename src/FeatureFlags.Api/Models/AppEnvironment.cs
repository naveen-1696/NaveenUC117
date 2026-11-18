using System.ComponentModel.DataAnnotations;

namespace FeatureFlags.Api.Models;

public class AppEnvironment
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = default!;

    public bool IsProtected { get; set; }

    public ICollection<FlagEnvironmentValue> FlagValues { get; set; } = new List<FlagEnvironmentValue>();
    public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
}
