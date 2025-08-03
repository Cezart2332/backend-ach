using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using WebApplication1.Models.Auth;

namespace WebApplication1.Services
{
    public interface IAuthService
    {
        Task<AuthResponseDto?> AuthenticateAsync(LoginRequestDto request, string ipAddress);
        Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
        Task<bool> LogoutAsync(string refreshToken, string ipAddress);
        Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, string ipAddress);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly ILogger<AuthService> _logger;
        private const int MaxFailedAttempts = 5;
        private const int LockoutDurationMinutes = 30;

        public AuthService(AppDbContext context, IJwtService jwtService, ILogger<AuthService> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<AuthResponseDto?> AuthenticateAsync(LoginRequestDto request, string ipAddress)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => (u.Username == request.Username || u.Email == request.Username) && u.IsActive);

            if (user == null)
            {
                _logger.LogWarning("Authentication failed: User not found. Username/Email: {Username}, IP: {IpAddress}", 
                    request.Username, ipAddress);
                return null;
            }

            // Check if account is locked
            if (user.IsLocked)
            {
                _logger.LogWarning("Authentication failed: Account locked. UserId: {UserId}, IP: {IpAddress}", 
                    user.Id, ipAddress);
                return null;
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                await HandleFailedLoginAsync(user, ipAddress);
                _logger.LogWarning("Authentication failed: Invalid password. UserId: {UserId}, IP: {IpAddress}", 
                    user.Id, ipAddress);
                return null;
            }

            // Reset failed login attempts on successful login
            await HandleSuccessfulLoginAsync(user, ipAddress);

            var authResponse = await _jwtService.GenerateTokensAsync(user);
            
            _logger.LogInformation("User authenticated successfully. UserId: {UserId}, IP: {IpAddress}", 
                user.Id, ipAddress);
            
            return authResponse;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            // Check if user already exists
            var existingUser = await _context.Users
                .AnyAsync(u => u.Username == request.Username || u.Email == request.Email);

            if (existingUser)
            {
                throw new ArgumentException("User with this username or email already exists");
            }

            var user = new User
            {
                Username = request.Username,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "User",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user registered. UserId: {UserId}, Email: {Email}", user.Id, user.Email);

            return await _jwtService.GenerateTokensAsync(user);
        }

        public async Task<bool> LogoutAsync(string refreshToken, string ipAddress)
        {
            return await _jwtService.RevokeTokenAsync(refreshToken, ipAddress);
        }

        public async Task<AuthResponseDto?> RefreshTokenAsync(string refreshToken, string ipAddress)
        {
            return await _jwtService.RefreshTokenAsync(refreshToken, ipAddress);
        }

        private async Task HandleFailedLoginAsync(User user, string ipAddress)
        {
            user.FailedLoginAttempts++;
            user.UpdatedAt = DateTime.UtcNow;

            if (user.FailedLoginAttempts >= MaxFailedAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);
                _logger.LogWarning("Account locked due to too many failed attempts. UserId: {UserId}, IP: {IpAddress}", 
                    user.Id, ipAddress);
            }

            await _context.SaveChangesAsync();
        }

        private async Task HandleSuccessfulLoginAsync(User user, string ipAddress)
        {
            user.FailedLoginAttempts = 0;
            user.LockedUntil = null;
            user.LastLoginAt = DateTime.UtcNow;
            user.LastLoginIp = ipAddress;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }
    }
}
