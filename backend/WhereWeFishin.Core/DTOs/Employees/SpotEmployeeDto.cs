namespace WhereWeFishin.Core.DTOs;

public class SpotEmployeeDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int FishingSpotId { get; set; }
    public string FishingSpotName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AssignEmployeeDto
{
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
}

public class VerifyQrDto
{
    public int BookingId { get; set; }
    public string VerificationToken { get; set; } = string.Empty;
}

public class QrVerificationResultDto
{
    public bool Valid { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? BookingId { get; set; }
    public string? Username { get; set; }
    public string? FishingSpotName { get; set; }
    public string? PontoonName { get; set; }
    public DateTime? StartDate { get; set; }
    public int? DurationHours { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Status { get; set; }
}
