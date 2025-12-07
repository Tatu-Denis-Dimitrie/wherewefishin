using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class LoginRequest
{
    [Required(ErrorMessage = "Username sau Email este obligatoriu")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola este obligatorie")]
    public string Password { get; set; } = string.Empty;
}
