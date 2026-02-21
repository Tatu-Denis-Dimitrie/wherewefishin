namespace WhereWeFishin.Core.DTOs;

public class BookingDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
    public string FishingSpotName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateBookingDto
{
    public int FishingSpotId { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
}

public class UpdateBookingStatusDto
{
    public string Status { get; set; } = string.Empty;
}
