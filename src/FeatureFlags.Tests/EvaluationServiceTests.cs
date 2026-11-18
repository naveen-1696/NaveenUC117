using FeatureFlags.Api.Data;
using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Models;
using FeatureFlags.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FeatureFlags.Tests;

public class EvaluationServiceTests
{
    private AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task EvaluateFlag_FlagDisabled_ReturnsFalse()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "my-flag", Name = "My Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();
        db.FlagEnvironmentValues.Add(new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = false, Value = "false" });
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);
        var result = await svc.EvaluateFlagAsync("test", "my-flag", new EvaluateRequest("user1", null));

        result!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateFlag_FlagEnabled_NoRules_ReturnsTrue()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "my-flag", Name = "My Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();
        db.FlagEnvironmentValues.Add(new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = true, Value = "true" });
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);
        var result = await svc.EvaluateFlagAsync("test", "my-flag", new EvaluateRequest("user1", null));

        result!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateFlag_UnknownFlag_ReturnsFalseDefault()
    {
        var db = CreateDb();
        var svc = new EvaluationService(db);
        var result = await svc.EvaluateFlagAsync("test", "nonexistent", new EvaluateRequest("user1", null));
        result.Should().NotBeNull();
        result!.IsEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("user-aaa")]
    [InlineData("user-bbb")]
    public void StableBucket_SameInput_GivesSameBucket(string userId)
    {
        var bucket1 = EvaluationService.GetStableBucket("my-flag", userId);
        var bucket2 = EvaluationService.GetStableBucket("my-flag", userId);
        bucket1.Should().Be(bucket2);
        bucket1.Should().BeInRange(0, 99);
    }

    [Fact]
    public void StableBucket_Distribution_IsReasonablyUniform()
    {
        // 1000 users → roughly uniform across 0-99
        var buckets = Enumerable.Range(0, 1000)
            .Select(i => EvaluationService.GetStableBucket("checkout-v2", $"user-{i}"))
            .ToList();

        var below20 = buckets.Count(b => b < 20);
        // With 1000 users, expect ~200 ± 50 to fall below 20
        below20.Should().BeInRange(150, 260);
    }

    [Fact]
    public async Task EvaluateFlag_PercentageRule_MatchesCorrectly()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "pct-flag", Name = "Pct Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();

        var fev = new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = true, Value = "false" };
        db.FlagEnvironmentValues.Add(fev);
        await db.SaveChangesAsync();

        // Add 100% rollout rule → everyone gets true
        db.TargetingRules.Add(new TargetingRule
        {
            FlagEnvironmentValueId = fev.Id,
            Type = RuleType.Percentage,
            Percentage = 100,
            Order = 1,
            ServeValue = "true"
        });
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);
        var result = await svc.EvaluateFlagAsync("test", "pct-flag", new EvaluateRequest("any-user", null));
        result!.RuleMatched.Should().Contain("Percentage");
    }

    [Fact]
    public async Task EvaluateFlag_AttributeRule_EqualsMatch()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "country-flag", Name = "Country Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();

        var fev = new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = true, Value = "false" };
        db.FlagEnvironmentValues.Add(fev);
        await db.SaveChangesAsync();

        db.TargetingRules.Add(new TargetingRule
        {
            FlagEnvironmentValueId = fev.Id,
            Type = RuleType.Attribute,
            AttributeKey = "country",
            Operator = RuleOperator.Equals,
            AttributeValue = "US",
            Order = 1,
            ServeValue = "true"
        });
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);

        var usResult = await svc.EvaluateFlagAsync("test", "country-flag",
            new EvaluateRequest("u1", new Dictionary<string, string> { ["country"] = "US" }));
        usResult!.RuleMatched.Should().NotBeNull();

        var ukResult = await svc.EvaluateFlagAsync("test", "country-flag",
            new EvaluateRequest("u2", new Dictionary<string, string> { ["country"] = "UK" }));
        ukResult!.RuleMatched.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateFlag_AttributeRule_InList()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "plan-flag", Name = "Plan Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();

        var fev = new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = true, Value = "false" };
        db.FlagEnvironmentValues.Add(fev);
        await db.SaveChangesAsync();

        db.TargetingRules.Add(new TargetingRule
        {
            FlagEnvironmentValueId = fev.Id,
            Type = RuleType.Attribute,
            AttributeKey = "plan",
            Operator = RuleOperator.InList,
            AttributeValue = "[\"premium\",\"enterprise\"]",
            Order = 1,
            ServeValue = "true"
        });
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);

        var premiumResult = await svc.EvaluateFlagAsync("test", "plan-flag",
            new EvaluateRequest("u1", new Dictionary<string, string> { ["plan"] = "premium" }));
        premiumResult!.RuleMatched.Should().NotBeNull();

        var freeResult = await svc.EvaluateFlagAsync("test", "plan-flag",
            new EvaluateRequest("u2", new Dictionary<string, string> { ["plan"] = "free" }));
        freeResult!.RuleMatched.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateFlag_RulesEvaluatedInOrder()
    {
        var db = CreateDb();
        var env = new AppEnvironment { Name = "test" };
        var flag = new Flag { Key = "order-flag", Name = "Order Flag", Type = FlagType.Boolean, CreatedByUserId = "u1" };
        db.Environments.Add(env);
        db.Flags.Add(flag);
        await db.SaveChangesAsync();

        var fev = new FlagEnvironmentValue { FlagId = flag.Id, EnvironmentId = env.Id, IsEnabled = true, Value = "false" };
        db.FlagEnvironmentValues.Add(fev);
        await db.SaveChangesAsync();

        // Order 1: Attribute country=US → true
        // Order 2: Percentage 100 → false (should never be reached for US users)
        db.TargetingRules.AddRange(
            new TargetingRule { FlagEnvironmentValueId = fev.Id, Type = RuleType.Attribute, AttributeKey = "country", Operator = RuleOperator.Equals, AttributeValue = "US", Order = 1, ServeValue = "true" },
            new TargetingRule { FlagEnvironmentValueId = fev.Id, Type = RuleType.Percentage, Percentage = 100, Order = 2, ServeValue = "false" }
        );
        await db.SaveChangesAsync();

        var svc = new EvaluationService(db);
        var result = await svc.EvaluateFlagAsync("test", "order-flag",
            new EvaluateRequest("u1", new Dictionary<string, string> { ["country"] = "US" }));

        result!.RuleMatched.Should().Contain("Attribute");
    }
}
