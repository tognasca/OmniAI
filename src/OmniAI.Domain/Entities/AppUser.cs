using System.Security.Cryptography;
using System.Text;

namespace OmniAI.Domain.Entities;

/// <summary>
/// Usuário do sistema (por tenant). Role: Admin | Agent
/// </summary>
public class AppUser : BaseEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Email { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Role { get; set; } = "Agent"; // "Admin" | "Agent"
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public Tenant Tenant { get; set; } = default!;

    // Helper: hash senha com PBKDF2
    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000,
            HashAlgorithmName.SHA256, 32);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string stored)
    {
        var parts = stored.Split(':');
        if (parts.Length != 2) return false;
        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] stored2 = Convert.FromBase64String(parts[1]);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, 100_000,
            HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(hash, stored2);
    }
}

/// <summary>
/// Refresh token para renovação de JWT sem re-login.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }

    public AppUser User { get; set; } = default!;
}