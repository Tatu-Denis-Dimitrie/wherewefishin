using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Core.Entities;

public class FishingSession : BaseEntity
{
    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
    public int? PontoonId { get; set; }
    public DateTime StartDate { get; set; }
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;
    public string? VerificationToken { get; set; }

    public User User { get; set; } = null!;
    public FishingSpot FishingSpot { get; set; } = null!;
    public Pontoon? Pontoon { get; set; }
}
