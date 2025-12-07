using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class RegisterRequest
{
    [Required(ErrorMessage = "Username-ul este obligatoriu")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username-ul trebuie să aibă între 3 și 50 de caractere")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email-ul este obligatoriu")]
    [EmailAddress(ErrorMessage = "Format de email invalid")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola este obligatorie")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Parola trebuie să aibă minim 6 caractere")]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Parolele nu coincid")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
