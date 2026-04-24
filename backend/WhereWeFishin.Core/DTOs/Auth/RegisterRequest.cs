using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "The username is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "The username must be between 3 and 50 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "The email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "The password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "The password must be at least 6 characters long")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "The passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
