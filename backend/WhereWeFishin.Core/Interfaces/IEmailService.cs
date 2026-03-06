namespace WhereWeFishin.Core.Interfaces;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string? firstName);
}
