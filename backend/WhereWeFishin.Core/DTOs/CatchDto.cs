namespace WhereWeFishin.Core.DTOs;

public class CatchDto
{
    public Guid Id { get; set; }
    public string FishSpecies { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public double? Length { get; set; }
    public DateTime CaughtAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }
    public Guid UserId { get; set; }
    public Guid FishingSpotId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateCatchDto
{
    public string FishSpecies { get; set; } = string.Empty;
    public double? Weight { get; set; }
    public double? Length { get; set; }
    public DateTime CaughtAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }
    public Guid FishingSpotId { get; set; }
}

public class UpdateCatchDto
{
    public string? FishSpecies { get; set; }
    public double? Weight { get; set; }
    public double? Length { get; set; }
    public DateTime? CaughtAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? Notes { get; set; }
}
