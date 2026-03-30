using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;

namespace OmniAI.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IOmniDbContext db, IConfiguration cfg) : ControllerBase
{
    // ── POST /api/auth/login ──────────────────────────────────────
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var user = await db.AppUsers
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Email == req.Email.ToLower() && u.IsActive, ct);

        if (user is null || !AppUser.VerifyPassword(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Email ou senha inválidos." });

        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var (accessToken, refreshToken) = GenerateTokens(user);

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Ok(new LoginResponse(
            accessToken, refreshToken,
            user.Id, user.Name, user.Email,
            user.Role, user.TenantId, user.Tenant.Name, user.Tenant.Slug));
    }

    // ── POST /api/auth/refresh ────────────────────────────────────
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var stored = await db.RefreshTokens
            .Include(r => r.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && !r.IsRevoked, ct);

        if (stored is null || stored.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "Token inválido ou expirado." });

        stored.IsRevoked = true;

        var (accessToken, newRefresh) = GenerateTokens(stored.User);
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = stored.UserId,
            Token = newRefresh,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return Ok(new { accessToken, refreshToken = newRefresh });
    }

    // ── POST /api/auth/logout ─────────────────────────────────────
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken, ct);
        if (stored is not null) stored.IsRevoked = true;
        await db.SaveChangesAsync(ct);
        return Ok();
    }

    // ── GET /api/auth/me ──────────────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();

        var user = await db.AppUsers
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId), ct);

        if (user is null) return NotFound();

        return Ok(new
        {
            user.Id,
            user.Name,
            user.Email,
            user.Role,
            user.TenantId,
            tenantName = user.Tenant.Name
        });
    }

    // ── POST /api/auth/register (Admin only) ─────────────────────
    [HttpPost("register")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        // Only SuperAdmin can create new tenants + admin users
        if (await db.AppUsers.AnyAsync(u => u.Email == req.Email.ToLower(), ct))
            return Conflict(new { error = "Email já cadastrado." });

        // Create tenant if tenantId not provided
        Guid tenantId = req.TenantId ?? Guid.NewGuid();
        if (req.TenantId is null)
        {
            var tenant = new Tenant
            {
                Id = tenantId,
                Name = req.TenantName ?? req.Email.Split('@')[0],
                Slug = (req.TenantName ?? req.Email.Split('@')[0])
                              .ToLower().Replace(" ", "-"),
                Plan = TenantPlan.Pro,
                IsActive = true,
            };
            db.Tenants.Add(tenant);
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = req.Email.ToLower(),
            Name = req.Name,
            PasswordHash = AppUser.HashPassword(req.Password),
            Role = req.Role ?? "Admin",
            IsActive = true,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Email, user.Name, user.Role, user.TenantId });
    }

    // ── POST /api/auth/users (Admin: add user to own tenant) ──────
    [HttpPost("users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> AddUser([FromBody] AddUserRequest req, CancellationToken ct)
    {
        var callerTenantId = Guid.Parse(User.FindFirstValue("tenantId")!);

        if (await db.AppUsers.AnyAsync(u => u.Email == req.Email.ToLower() && u.TenantId == callerTenantId, ct))
            return Conflict(new { error = "Email já cadastrado neste tenant." });

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            TenantId = callerTenantId,
            Email = req.Email.ToLower(),
            Name = req.Name,
            PasswordHash = AppUser.HashPassword(req.Password),
            Role = req.Role ?? "Agent",
            IsActive = true,
        };
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);

        return Ok(new { user.Id, user.Email, user.Name, user.Role });
    }

    // ── GET /api/auth/users (Admin: list own tenant users) ────────
    [HttpGet("users")]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var callerTenantId = Guid.Parse(User.FindFirstValue("tenantId")!);
        var users = await db.AppUsers
            .Where(u => u.TenantId == callerTenantId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.IsActive, u.LastLoginAt })
            .ToListAsync(ct);
        return Ok(users);
    }

    // ── GET /api/auth/tenants (SuperAdmin only) ───────────────────
    [HttpGet("tenants")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> GetTenants(CancellationToken ct)
    {
        var tenants = await db.Tenants
            .Select(t => new { t.Id, t.Name, t.Slug, t.Plan, t.IsActive, t.CreatedAt })
            .ToListAsync(ct);
        return Ok(tenants);
    }

    // ── Helpers ───────────────────────────────────────────────────
    private (string AccessToken, string RefreshToken) GenerateTokens(AppUser user)
    {
        var key = cfg["Jwt:Key"] ?? "omni-ai-dev-secret-key-change-in-production-32chars";
        var issuer = cfg["Jwt:Issuer"] ?? "OmniAI";
        var secKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var creds = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.Name),
            new Claim(ClaimTypes.Role,           user.Role),
            new Claim("tenantId",                user.TenantId.ToString()),
        };

        var accessToken = new JwtSecurityToken(
            issuer: issuer,
            audience: "omni-ai-app",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        return (new JwtSecurityTokenHandler().WriteToken(accessToken), refreshToken);
    }
}

// ── Request / Response records ─────────────────────────────────────
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record LoginResponse(
    string AccessToken, string RefreshToken,
    Guid UserId, string Name, string Email, string Role,
    Guid TenantId, string TenantName, string TenantSlug);
public record RegisterRequest(
    string Email, string Password, string Name,
    string? TenantName, Guid? TenantId, string? Role);
public record AddUserRequest(string Email, string Password, string Name, string? Role);