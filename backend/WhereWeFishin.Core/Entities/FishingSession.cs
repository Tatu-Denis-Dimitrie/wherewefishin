namespace WhereWeFishin.Core.Entities;

public enum SessionStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public class FishingSession : BaseEntity
{
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;

    public User User { get; set; } = null!;
    public FishingSpot FishingSpot { get; set; } = null!;
}
