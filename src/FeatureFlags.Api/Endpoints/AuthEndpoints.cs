using FeatureFlags.Api.DTOs;
using FeatureFlags.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace FeatureFlags.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest req, UserManager<ApplicationUser> userManager, IConfiguration config) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user == null || !await userManager.CheckPasswordAsync(user, req.Password))
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault() ?? "Developer";
            var token = GenerateToken(user, role, config);
            return Results.Ok(new LoginResponse(token, user.Id, user.Email!, role));
        });

        group.MapPost("/register", async (RegisterRequest req, UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager, ClaimsPrincipal caller) =>
        {
            if (!caller.IsInRole("Admin")) return Results.Forbid();

            var validRoles = new[] { "Admin", "Approver", "Developer" };
            if (!validRoles.Contains(req.Role)) return Results.BadRequest(new ErrorResponse("Invalid role"));

            var user = new ApplicationUser { UserName = req.Email, Email = req.Email, FullName = req.FullName, EmailConfirmed = true };
            var result = await userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new ErrorResponse("Registration failed", string.Join(", ", result.Errors.Select(e => e.Description))));

            await userManager.AddToRoleAsync(user, req.Role);
            return Results.Ok(new { user.Id, user.Email });
        }).RequireAuthorization();
    }

    private static string GenerateToken(ApplicationUser user, string role, IConfiguration config)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(ClaimTypes.Role, role),
            new Claim("fullName", user.FullName)
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"],
            audience: config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
