using Microsoft.AspNetCore.Identity;

namespace FeatureFlags.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
}
