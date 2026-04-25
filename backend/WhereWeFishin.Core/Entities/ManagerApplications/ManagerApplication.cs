using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Core.Entities;

public class ManagerApplication : BaseEntity
{
    public int ApplicantUserId { get; set; }
    public string LakeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? LocationLabel { get; set; }
    public decimal ProposedPricePerHour { get; set; }
    public string? FishSpecies { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string Motivation { get; set; } = string.Empty;
    public string AdministrationBasis { get; set; } = string.Empty;
    public ManagerApplicationStatus Status { get; set; } = ManagerApplicationStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public int? ApprovedFishingSpotId { get; set; }

    public User ApplicantUser { get; set; } = null!;
    public User? ReviewedByAdmin { get; set; }
    public FishingSpot? ApprovedFishingSpot { get; set; }
}