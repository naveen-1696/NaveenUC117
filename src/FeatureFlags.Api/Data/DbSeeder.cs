using FeatureFlags.Api.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FeatureFlags.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db, UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
    {
        await SeedRolesAsync(roleManager);
        await SeedUsersAsync(userManager);
        await SeedEnvironmentsAsync(db);
        await SeedFlagsAsync(db, userManager);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { "Admin", "Approver", "Developer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private static async Task SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        var users = new[]
        {
            ("admin@flagconsole.dev", "Admin@123!", "Admin Console", "Admin"),
            ("approver@flagconsole.dev", "Approver@123!", "Flag Approver", "Approver"),
            ("dev1@flagconsole.dev", "Dev@123!", "Alice Developer", "Developer"),
            ("dev2@flagconsole.dev", "Dev@123!", "Bob Developer", "Developer"),
        };

        foreach (var (email, password, fullName, role) in users)
        {
            if (await userManager.FindByEmailAsync(email) == null)
            {
                var user = new ApplicationUser { UserName = email, Email = email, FullName = fullName, EmailConfirmed = true };
                await userManager.CreateAsync(user, password);
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    private static async Task SeedEnvironmentsAsync(AppDbContext db)
    {
        if (db.Environments.Any()) return;

        var envs = new[]
        {
            new AppEnvironment { Name = "dev", IsProtected = false },
            new AppEnvironment { Name = "test", IsProtected = false },
            new AppEnvironment { Name = "prod", IsProtected = true },
        };
        db.Environments.AddRange(envs);
        await db.SaveChangesAsync();

        // Seed API keys (plain keys shown here for dev reference only)
        var apiKeyPlainValues = new Dictionary<string, string>
        {
            ["dev"] = "dev-api-key-00000000000000000001",
            ["test"] = "test-api-key-0000000000000000002",
            ["prod"] = "prod-api-key-0000000000000000003",
        };

        foreach (var env in db.Environments.ToList())
        {
            if (apiKeyPlainValues.TryGetValue(env.Name, out var plain))
            {
                db.ApiKeys.Add(new ApiKey
                {
                    EnvironmentId = env.Id,
                    Name = $"{env.Name}-default",
                    KeyHash = HashApiKey(plain),
                    IsActive = true
                });
            }
        }
        await db.SaveChangesAsync();
    }

    private static async Task SeedFlagsAsync(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        if (db.Flags.Any()) return;

        var adminUser = await userManager.FindByEmailAsync("admin@flagconsole.dev");
        var devUser = await userManager.FindByEmailAsync("dev1@flagconsole.dev");
        var envs = db.Environments.ToList();
        var devEnv = envs.First(e => e.Name == "dev");
        var testEnv = envs.First(e => e.Name == "test");
        var prodEnv = envs.First(e => e.Name == "prod");

        var flagDefs = new[]
        {
            ("dark-mode", "Dark Mode", "Enable dark mode UI", FlagType.Boolean, "true"),
            ("checkout-v2", "Checkout V2", "New checkout flow", FlagType.Boolean, "false"),
            ("max-upload-mb", "Max Upload MB", "Maximum file upload size", FlagType.Number, "10"),
            ("banner-text", "Banner Text", "Top banner message", FlagType.String, "\"Welcome to FlagConsole\""),
            ("new-dashboard", "New Dashboard", "New analytics dashboard", FlagType.Boolean, "false"),
            ("api-rate-limit", "API Rate Limit", "Requests per minute limit", FlagType.Number, "100"),
            ("beta-search", "Beta Search", "Beta AI search feature", FlagType.Boolean, "false"),
            ("maintenance-mode", "Maintenance Mode", "Put app in maintenance", FlagType.Boolean, "false"),
            ("theme-color", "Theme Color", "Primary theme color", FlagType.String, "\"#0D8390\""),
            ("multi-currency", "Multi Currency", "Enable multi-currency support", FlagType.Boolean, "false"),
        };

        foreach (var (key, name, desc, type, defaultVal) in flagDefs)
        {
            var flag = new Flag
            {
                Key = key,
                Name = name,
                Description = desc,
                Type = type,
                CreatedByUserId = adminUser!.Id
            };
            db.Flags.Add(flag);
            await db.SaveChangesAsync();

            foreach (var env in envs)
            {
                var isEnabled = key == "dark-mode" || (key == "checkout-v2" && env.Name == "dev");
                var fev = new FlagEnvironmentValue
                {
                    FlagId = flag.Id,
                    EnvironmentId = env.Id,
                    IsEnabled = isEnabled,
                    Value = defaultVal
                };
                db.FlagEnvironmentValues.Add(fev);
                await db.SaveChangesAsync();

                // Add sample rules for checkout-v2
                if (key == "checkout-v2" && env.Name == "dev")
                {
                    db.TargetingRules.Add(new TargetingRule
                    {
                        FlagEnvironmentValueId = fev.Id,
                        Type = RuleType.Percentage,
                        Percentage = 20,
                        Order = 1,
                        ServeValue = "true"
                    });
                    db.TargetingRules.Add(new TargetingRule
                    {
                        FlagEnvironmentValueId = fev.Id,
                        Type = RuleType.Attribute,
                        AttributeKey = "country",
                        Operator = RuleOperator.Equals,
                        AttributeValue = "US",
                        Order = 2,
                        ServeValue = "true"
                    });
                    db.TargetingRules.Add(new TargetingRule
                    {
                        FlagEnvironmentValueId = fev.Id,
                        Type = RuleType.Attribute,
                        AttributeKey = "plan",
                        Operator = RuleOperator.InList,
                        AttributeValue = "[\"premium\",\"enterprise\"]",
                        Order = 3,
                        ServeValue = "true"
                    });
                    await db.SaveChangesAsync();
                }

                // Record initial version
                var snapshot = JsonSerializer.Serialize(new
                {
                    IsEnabled = isEnabled,
                    Value = defaultVal,
                    Rules = new object[] { }
                });
                db.FlagVersions.Add(new FlagVersion
                {
                    FlagId = flag.Id,
                    EnvironmentId = env.Id,
                    Version = 1,
                    ChangedByUserId = adminUser!.Id,
                    Reason = "Initial seed",
                    Snapshot = snapshot
                });
            }
            await db.SaveChangesAsync();
        }
    }

    public static string HashApiKey(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
