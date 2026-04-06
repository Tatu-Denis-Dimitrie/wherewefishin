namespace WhereWeFishin.Core.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
    public string FishingSpotName { get; set; } = string.Empty;
    public int? PontoonId { get; set; }
    public string? PontoonName { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? VerificationToken { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateBookingDto
{
    public int FishingSpotId { get; set; }
    public int? PontoonId { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
    public string? PaymentIntentId { get; set; }
}

public class UpdateBookingStatusDto
{
    public string Status { get; set; } = string.Empty;
}

public class CreatePaymentIntentDto
{
    public int FishingSpotId { get; set; }
    public int? PontoonId { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
}

public class PaymentIntentDto
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string Currency { get; set; } = "ron";
}

public class PaymentConfigurationDto
{
    public bool StripeEnabled { get; set; }
}

public class BookedPeriodDto
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
