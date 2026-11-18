using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Services;
using Microsoft.Net.Http.Headers;

namespace FeatureFlags.Api.Endpoints;

public static class EvaluationEndpoints
{
    public static void MapEvaluationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/evaluate").WithTags("Evaluation");

        // Evaluate a single flag
        group.MapPost("/{environmentName}/{flagKey}", async (
            string environmentName, string flagKey,
            EvaluateRequest req,
            EvaluationService svc,
            ApiKeyService apiKeySvc,
            HttpContext ctx) =>
        {
            if (!await ValidateApiKey(ctx, apiKeySvc, environmentName))
                return Results.Json(new ErrorResponse("Invalid or missing API key"), statusCode: 401);

            var result = await svc.EvaluateFlagAsync(environmentName, flagKey, req);
            return result == null
                ? Results.Json(new EvaluateResponse(flagKey, false, null, null))
                : Results.Ok(result);
        });

        // Snapshot endpoint with ETag
        group.MapGet("/{environmentName}/snapshot", async (
            string environmentName,
            EvaluationService svc,
            ApiKeyService apiKeySvc,
            HttpContext ctx) =>
        {
            if (!await ValidateApiKey(ctx, apiKeySvc, environmentName))
                return Results.Json(new ErrorResponse("Invalid or missing API key"), statusCode: 401);

            var (flags, etag) = await svc.GetSnapshotAsync(environmentName);

            var requestEtag = ctx.Request.Headers[HeaderNames.IfNoneMatch].FirstOrDefault();
            if (requestEtag == etag)
                return Results.StatusCode(304);

            ctx.Response.Headers[HeaderNames.ETag] = etag;
            ctx.Response.Headers["Cache-Control"] = "no-cache";
            return Results.Ok(flags);
        });
    }

    private static async Task<bool> ValidateApiKey(HttpContext ctx, ApiKeyService apiKeySvc, string environmentName)
    {
        string? key = null;

        // Accept key from Authorization: Bearer <key> or X-Api-Key header
        var auth = ctx.Request.Headers["Authorization"].FirstOrDefault();
        if (auth?.StartsWith("Bearer ") == true)
            key = auth[7..];
        else
            key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(key)) return false;

        var env = await apiKeySvc.ValidateApiKeyAsync(key, environmentName);
        return env != null;
    }
}
