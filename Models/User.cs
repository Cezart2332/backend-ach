using System.ComponentModel.DataAnnotations.Schema;
using WebApplication1.Models.Auth;

namespace WebApplication1.Models;

[Table("users")]
public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public byte[] ProfileImage { get; set; } = Array.Empty<byte>();
    public string Role { get; set; } = "User";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockedUntil { get; set; }

    // Navigation properties
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public bool IsLocked => LockedUntil.HasValue && LockedUntil > DateTime.UtcNow;
}