using System.ComponentModel.DataAnnotations;

namespace WhereWeFishin.Core.DTOs;

public class ManagerApplicationDto
{
    public int Id { get; set; }
    public int ApplicantUserId { get; set; }
    public string ApplicantUsername { get; set; } = string.Empty;
    public string ApplicantDisplayName { get; set; } = string.Empty;
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
    public string Status { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int? ReviewedByAdminId { get; set; }
    public string? ReviewedByAdminName { get; set; }
    public int? ApprovedFishingSpotId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateManagerApplicationDto
{
    [Required]
    [MaxLength(100)]
    public string LakeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(250)]
    public string? LocationLabel { get; set; }

    [Range(0, 100_000)]
    public decimal ProposedPricePerHour { get; set; }

    public string? FishSpecies { get; set; }

    [Required]
    [MaxLength(10)]
    [RegularExpression(@"^\d{1,10}$")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(1500)]
    public string Motivation { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string AdministrationBasis { get; set; } = string.Empty;
}

public class UpdateManagerApplicationDto
{
    [Required]
    [MaxLength(100)]
    public string LakeName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [MaxLength(250)]
    public string? LocationLabel { get; set; }

    [Range(0, 100_000)]
    public decimal ProposedPricePerHour { get; set; }

    public string? FishSpecies { get; set; }

    [Required]
    [MaxLength(10)]
    [RegularExpression(@"^\d{1,10}$")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required]
    [MaxLength(1500)]
    public string Motivation { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string AdministrationBasis { get; set; } = string.Empty;
}

public class RejectManagerApplicationDto
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
}