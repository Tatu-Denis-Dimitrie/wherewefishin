namespace WhereWeFishin.Core.Entities;

public class ImageAnalysis : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string? ProcessedImageUrl { get; set; }
    public int TotalDetections { get; set; }
    public string? DominantFishType { get; set; }
    public double DominantConfidence { get; set; }
    public string? DetectionsJson { get; set; }
    public DateTime AnalyzedAt { get; set; }
}
