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
        
        // Company-specific methods
        Task<AuthResponseDto?> AuthenticateCompanyAsync(CompanyLoginRequestDto request, string ipAddress);
        Task<AuthResponseDto> RegisterCompanyAsync(CompanyRegisterRequestDto request);
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
            // First, try to authenticate as a user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => (u.Username == request.Username || u.Email == request.Username) && u.IsActive);

            if (user != null)
            {
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

            // If user authentication fails, try to authenticate as a company
            var company = await _context.Companies
                .FirstOrDefaultAsync(c => c.Email == request.Username);

            if (company != null)
            {
                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, company.Password))
                {
                    _logger.LogWarning("Authentication failed: Invalid company password. CompanyId: {CompanyId}, IP: {IpAddress}", 
                        company.Id, ipAddress);
                    return null;
                }

                var authResponse = await _jwtService.GenerateTokensForCompanyAsync(company);
                
                _logger.LogInformation("Company authenticated successfully. CompanyId: {CompanyId}, IP: {IpAddress}", 
                    company.Id, ipAddress);
                
                return authResponse;
            }

            _logger.LogWarning("Authentication failed: User/Company not found. Username/Email: {Username}, IP: {IpAddress}", 
                request.Username, ipAddress);
            return null;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
        {
            // Check if user already exists
            var existingUser = await _context.Users
                .AnyAsync(u => u.Username == request.Username || u.Email == request.Email);

            // Check if company already exists
            var existingCompany = await _context.Companies
                .AnyAsync(c => c.Email == request.Email);

            if (existingUser || existingCompany)
            {
                throw new ArgumentException("User or company with this username or email already exists");
            }

            // If FirstName is empty and LastName is empty, treat as company registration
            if (string.IsNullOrEmpty(request.FirstName) && string.IsNullOrEmpty(request.LastName))
            {
                // Register as company
                var company = new Company
                {
                    Name = request.Username,
                    Email = request.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                _logger.LogInformation("New company registered. CompanyId: {CompanyId}, Email: {Email}", 
                    company.Id, company.Email);

                return await _jwtService.GenerateTokensForCompanyAsync(company);
            }
            else
            {
                // Register as user
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

                _logger.LogInformation("New user registered. UserId: {UserId}, Email: {Email}", 
                    user.Id, user.Email);

                return await _jwtService.GenerateTokensAsync(user);
            }
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

        public async Task<AuthResponseDto?> AuthenticateCompanyAsync(CompanyLoginRequestDto request, string ipAddress)
        {
            try
            {
                _logger.LogInformation("Company authentication attempt - Email: {Email}, IP: {IpAddress}", 
                    request.Email, ipAddress);

                var company = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Email == request.Email);

                if (company == null)
                {
                    _logger.LogWarning("Company authentication failed: Company not found. Email: {Email}, IP: {IpAddress}", 
                        request.Email, ipAddress);
                    return null;
                }

                _logger.LogInformation("Company found, verifying password - CompanyId: {CompanyId}", company.Id);

                if (!BCrypt.Net.BCrypt.Verify(request.Password, company.Password))
                {
                    _logger.LogWarning("Company authentication failed: Invalid password. CompanyId: {CompanyId}, IP: {IpAddress}", 
                        company.Id, ipAddress);
                    return null;
                }

                _logger.LogInformation("Password verified, generating tokens - CompanyId: {CompanyId}", company.Id);

                var authResponse = await _jwtService.GenerateTokensForCompanyAsync(company);
                
                _logger.LogInformation("Company authenticated successfully. CompanyId: {CompanyId}, IP: {IpAddress}", 
                    company.Id, ipAddress);

                return authResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during company authentication for email: {Email}, IP: {IpAddress}", 
                    request.Email, ipAddress);
                throw;
            }
        }

        public async Task<AuthResponseDto> RegisterCompanyAsync(CompanyRegisterRequestDto request)
        {
            try
            {
                _logger.LogInformation("Starting company registration for email: {Email}", request.Email);

                // Check if company already exists
                var existingCompany = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Email == request.Email);

                if (existingCompany != null)
                {
                    _logger.LogWarning("Company registration failed: Email already exists - {Email}", request.Email);
                    throw new ArgumentException("Company with this email already exists");
                }

                // Parse CUI safely
                int cuiValue = 0;
                if (!string.IsNullOrEmpty(request.Cui))
                {
                    if (!int.TryParse(request.Cui, out cuiValue))
                    {
                        _logger.LogWarning("Company registration: Invalid CUI format - {Cui}", request.Cui);
                        // Continue with CUI = 0 instead of failing
                    }
                }

                // Create new company
                var company = new Company
                {
                    Name = request.Name,
                    Email = request.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    Description = request.Description ?? string.Empty,
                    Cui = cuiValue,
                    Category = request.Category ?? string.Empty,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _logger.LogInformation("Company object created, adding to database - Email: {Email}", request.Email);

                _context.Companies.Add(company);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Company saved to database - CompanyId: {CompanyId}", company.Id);

                // Generate tokens for the new company
                var authResponse = await _jwtService.GenerateTokensForCompanyAsync(company);

                _logger.LogInformation("Company registered successfully. CompanyId: {CompanyId}", company.Id);

                return authResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during company registration for email: {Email}", request.Email);
                throw;
            }
        }
    }
}
