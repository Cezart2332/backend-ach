using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using WebApplication1.Models;
using WebApplication1.Models.Auth;
using Microsoft.EntityFrameworkCore;

namespace WebApplication1.Services
{
    public interface IJwtService
    {
        Task<AuthResponseDto> GenerateTokensAsync(User user);
        Task<AuthResponseDto> GenerateTokensForCompanyAsync(Company company);
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress);
        ClaimsPrincipal? ValidateToken(string token);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;
        private readonly ILogger<JwtService> _logger;

        public JwtService(IConfiguration configuration, AppDbContext context, ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _context = context;
            _logger = logger;
        }

        public async Task<AuthResponseDto> GenerateTokensAsync(User user)
        {
            var jwtId = Guid.NewGuid().ToString();
            var accessToken = GenerateAccessToken(user, jwtId);
            var refreshToken = await GenerateRefreshTokenAsync(user.Id, jwtId);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email,
                    Role = "User", // You can extend this based on your user roles
                    ProfileImage = GetUserProfileImageBase64(user),
                    Scopes = new List<string> { "read", "write" } // Define based on user permissions
                },
                Company = null // No company data for user auth
            };
        }

        public async Task<AuthResponseDto> GenerateTokensForCompanyAsync(Company company)
        {
            var jwtId = Guid.NewGuid().ToString();
            var accessToken = GenerateAccessTokenForCompany(company, jwtId);
            var refreshToken = await GenerateRefreshTokenForCompanyAsync(company.Id, jwtId);

            return new AuthResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                User = null, // No user data for company auth
                Company = new CompanyDto
                {
                    Id = company.Id,
                    Name = company.Name,
                    Email = company.Email,
                    Description = company.Description,
                    Cui = company.Cui,
                    Category = company.Category,
                    Role = "Company",
                    Scopes = new List<string> { "read", "write", "manage" },
                    CreatedAt = company.CreatedAt,
                    IsActive = company.IsActive
                }
            };
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null || !storedToken.IsActive)
            {
                _logger.LogWarning("Invalid or expired refresh token used from IP: {IpAddress}", ipAddress);
                return null;
            }

            // Revoke the old token
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = ipAddress;

            // Generate new tokens
            var authResponse = await GenerateTokensAsync(storedToken.User);
            
            // Mark the old token as replaced
            storedToken.ReplacedByToken = authResponse.RefreshToken;
            
            await _context.SaveChangesAsync();

            _logger.LogInformation("Tokens refreshed successfully for user {UserId} from IP: {IpAddress}", 
                storedToken.UserId, ipAddress);

            return authResponse;
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken, string ipAddress)
        {
            var storedToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

            if (storedToken == null || !storedToken.IsActive)
                return false;

            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            storedToken.RevokedByIp = ipAddress;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Refresh token revoked for user {UserId} from IP: {IpAddress}", 
                storedToken.UserId, ipAddress);

            return true;
        }

        public ClaimsPrincipal? ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = _configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Token validation failed");
                return null;
            }
        }

        private string GenerateAccessToken(User user, string jwtId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, jwtId),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim("username", user.Username),
                new Claim("firstName", user.FirstName),
                new Claim("lastName", user.LastName),
                new Claim("role", "User"), // Extend based on your role system
                new Claim("scope", "read write") // Define based on user permissions
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task<RefreshToken> GenerateRefreshTokenAsync(int userId, string jwtId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                JwtId = jwtId,
                UserId = userId,
                CompanyId = null, // No company for user tokens
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Longer-lived refresh token
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }

        private string GenerateAccessTokenForCompany(Company company, string jwtId)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured"));

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, company.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, company.Email),
                new Claim(JwtRegisteredClaimNames.Jti, jwtId),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim("username", company.Name),
                new Claim("firstName", company.Name),
                new Claim("lastName", ""),
                new Claim("role", "Company"),
                new Claim("scope", "read write manage")
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(15), // Short-lived access token
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task<RefreshToken> GenerateRefreshTokenForCompanyAsync(int companyId, string jwtId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                JwtId = jwtId,
                CompanyId = companyId, // Use CompanyId field for companies
                UserId = null, // No user for company tokens
                ExpiresAt = DateTime.UtcNow.AddDays(7) // Longer-lived refresh token
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();

            return refreshToken;
        }
        private string? GetUserProfileImageBase64(User user)
        {
            if (user.ProfileImage == null || user.ProfileImage.Length == 0)
            {
                return null;
            }
            
            var base64 = Convert.ToBase64String(user.ProfileImage);
            return base64;
        }
    }
}
