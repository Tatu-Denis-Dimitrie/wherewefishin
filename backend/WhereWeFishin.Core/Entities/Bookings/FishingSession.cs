using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Core.Entities;

public class FishingSession : BaseEntity
{
    private DateTime _startDate;

    public int UserId { get; set; }
    public int FishingSpotId { get; set; }
    public int? PontoonId { get; set; }
    public DateTime StartDate
    {
        get => _startDate;
        set => _startDate = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
    public int DurationHours { get; set; }
    public decimal TotalPrice { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Pending;
    public string? VerificationToken { get; set; }

    public User User { get; set; } = null!;
    public FishingSpot FishingSpot { get; set; } = null!;
    public Pontoon? Pontoon { get; set; }
}
