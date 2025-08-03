using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models.Auth
{
    public class LoginRequestDto
    {
        [Required(ErrorMessage = "Username or email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;
    }

    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "Refresh token is required")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class AuthResponseDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserDto User { get; set; } = new();
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public List<string> Scopes { get; set; } = new();
    }

    public class RegisterRequestDto
    {
        [Required(ErrorMessage = "Username is required")]
        [MinLength(3, ErrorMessage = "Username must be at least 3 characters")]
        [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required")]
        [MaxLength(50, ErrorMessage = "First name cannot exceed 50 characters")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [MaxLength(50, ErrorMessage = "Last name cannot exceed 50 characters")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$", 
            ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one digit, and one special character")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [Phone(ErrorMessage = "Invalid phone number format")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}
