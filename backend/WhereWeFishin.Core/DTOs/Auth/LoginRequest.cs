using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Username or Email is required")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}
