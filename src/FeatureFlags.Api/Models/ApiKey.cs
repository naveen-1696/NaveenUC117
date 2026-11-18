namespace FeatureFlags.Api.Models;

public class ApiKey
{
    public int Id { get; set; }
    public int EnvironmentId { get; set; }
    public string Name { get; set; } = default!;
    public string KeyHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppEnvironment Environment { get; set; } = default!;
}
