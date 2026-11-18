using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FeatureFlags.Api.Endpoints;

public static class FlagEndpoints
{
    public static void MapFlagEndpoints(this WebApplication app)
    {
        var flags = app.MapGroup("/api/flags").RequireAuthorization().WithTags("Flags");
        var envs = app.MapGroup("/api/environments").RequireAuthorization().WithTags("Environments");
        var crs = app.MapGroup("/api/change-requests").RequireAuthorization().WithTags("ChangeRequests");
        var keys = app.MapGroup("/api/api-keys").RequireAuthorization().WithTags("ApiKeys");

        // Flags CRUD
        flags.MapGet("/", async (FlagService svc, int page = 1, int pageSize = 20, string? search = null) =>
            Results.Ok(await svc.GetFlagsAsync(page, pageSize, search)));

        flags.MapGet("/{id:int}", async (int id, FlagService svc) =>
        {
            var flag = await svc.GetFlagAsync(id);
            return flag == null ? Results.NotFound(new ErrorResponse("Flag not found")) : Results.Ok(flag);
        });

        flags.MapPost("/", async (CreateFlagRequest req, FlagService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin") && !user.IsInRole("Developer"))
                return Results.Forbid();
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
            var (flag, error) = await svc.CreateFlagAsync(req, userId);
            return error != null ? Results.BadRequest(new ErrorResponse(error)) : Results.Created($"/api/flags/{flag!.Id}", flag);
        });

        flags.MapPut("/{id:int}", async (int id, UpdateFlagRequest req, FlagService svc, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
            var (flag, error) = await svc.UpdateFlagAsync(id, req, userId);
            if (error == "CONFLICT") return Results.Conflict(new ErrorResponse("Concurrent edit detected. Reload and retry."));
            return error != null ? Results.NotFound(new ErrorResponse(error)) : Results.Ok(flag);
        });

        flags.MapDelete("/{id:int}", async (int id, FlagService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin")) return Results.Forbid();
            var error = await svc.ArchiveFlagAsync(id);
            return error != null ? Results.NotFound(new ErrorResponse(error)) : Results.NoContent();
        });

        // Per-environment value
        flags.MapGet("/{flagId:int}/environments/{envId:int}", async (int flagId, int envId, FlagService svc) =>
        {
            var (val, error) = await svc.GetFlagEnvironmentValueAsync(flagId, envId);
            return error != null ? Results.NotFound(new ErrorResponse(error)) : Results.Ok(val);
        });

        flags.MapPut("/{flagId:int}/environments/{envId:int}", async (int flagId, int envId,
            UpdateFlagEnvironmentRequest req, FlagService svc, ClaimsPrincipal user) =>
        {
            var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
            var isAdmin = user.IsInRole("Admin");
            var (result, error) = await svc.UpdateFlagEnvironmentAsync(flagId, envId, req, userId, isAdmin);
            if (error == "CONFLICT") return Results.Conflict(new ErrorResponse("Concurrent edit detected. Reload and retry."));
            return error != null ? Results.BadRequest(new ErrorResponse(error)) : Results.Ok(result);
        });

        // History & rollback
        flags.MapGet("/{flagId:int}/environments/{envId:int}/history", async (int flagId, int envId, FlagService svc) =>
            Results.Ok(await svc.GetHistoryAsync(flagId, envId)));

        flags.MapPost("/{flagId:int}/environments/{envId:int}/rollback/{versionId:int}",
            async (int flagId, int envId, int versionId, FlagService svc, ClaimsPrincipal user) =>
            {
                if (!user.IsInRole("Admin")) return Results.Forbid();
                var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
                var (result, error) = await svc.RollbackAsync(flagId, envId, versionId, userId);
                return error != null ? Results.BadRequest(new ErrorResponse(error)) : Results.Ok(result);
            });

        // Environments
        envs.MapGet("/", async (FeatureFlags.Api.Data.AppDbContext db) =>
        {
            var list = await db.Environments.AsNoTracking().ToListAsync();
            return Results.Ok(list.Select(e => new EnvironmentDto(e.Id, e.Name, e.IsProtected)));
        });

        envs.MapPost("/", async (EnvironmentDto req, FeatureFlags.Api.Data.AppDbContext db, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin")) return Results.Forbid();
            var env = new FeatureFlags.Api.Models.AppEnvironment { Name = req.Name, IsProtected = req.IsProtected };
            db.Environments.Add(env);
            await db.SaveChangesAsync();
            return Results.Created($"/api/environments/{env.Id}", new EnvironmentDto(env.Id, env.Name, env.IsProtected));
        });

        // Change requests
        crs.MapGet("/", async (FlagService svc, string? status) =>
        {
            if (status == "pending")
                return Results.Ok(await svc.GetPendingChangeRequestsAsync());
            return Results.Ok(await svc.GetAllChangeRequestsAsync());
        });

        crs.MapPost("/{id:int}/approve", async (int id, ReviewChangeRequest req, FlagService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Approver") && !user.IsInRole("Admin")) return Results.Forbid();
            var reviewerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
            var (result, error) = await svc.ApproveChangeRequestAsync(id, reviewerId, req.ReviewNote);
            return error != null ? Results.BadRequest(new ErrorResponse(error)) : Results.Ok(result);
        });

        crs.MapPost("/{id:int}/reject", async (int id, ReviewChangeRequest req, FlagService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Approver") && !user.IsInRole("Admin")) return Results.Forbid();
            var reviewerId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value!;
            var (result, error) = await svc.RejectChangeRequestAsync(id, reviewerId, req.ReviewNote);
            return error != null ? Results.BadRequest(new ErrorResponse(error)) : Results.Ok(result);
        });

        // API Keys (admin only)
        keys.MapGet("/", async (ApiKeyService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin")) return Results.Forbid();
            return Results.Ok(await svc.GetApiKeysAsync());
        });

        keys.MapPost("/", async (CreateApiKeyRequest req, ApiKeyService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin")) return Results.Forbid();
            var result = await svc.CreateApiKeyAsync(req);
            return Results.Ok(result);
        });

        keys.MapDelete("/{id:int}", async (int id, ApiKeyService svc, ClaimsPrincipal user) =>
        {
            if (!user.IsInRole("Admin")) return Results.Forbid();
            var ok = await svc.RevokeAsync(id);
            return ok ? Results.NoContent() : Results.NotFound();
        });
    }
}
