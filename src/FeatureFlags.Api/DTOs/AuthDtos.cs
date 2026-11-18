namespace FeatureFlags.Api.DTOs;

public record LoginRequest(string Email, string Password);

public record LoginResponse(string Token, string UserId, string Email, string Role);

public record RegisterRequest(string Email, string Password, string FullName, string Role);
