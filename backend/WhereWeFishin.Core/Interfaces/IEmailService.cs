namespace WhereWeFishin.Core.Interfaces;

public interface IEmailService
{
    Task SendWelcomeEmailAsync(string toEmail, string? firstName);
    Task SendBookingConfirmationEmailAsync(
        string toEmail,
        string? firstName,
        string spotName,
        DateTime startDateUtc,
        int durationHours,
        decimal totalPrice,
        int bookingId);

    Task SendPasswordResetEmailAsync(string toEmail, string? firstName, string resetCode);
}
