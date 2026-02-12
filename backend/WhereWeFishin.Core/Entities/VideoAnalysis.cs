namespace WhereWeFishin.Core.Entities;

public class VideoAnalysis : BaseEntity
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public string FileName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? ProcessedVideoUrl { get; set; }
    public double Duration { get; set; }
    public int TotalFrames { get; set; }
    public int Fps { get; set; }
    
    public int TotalDetections { get; set; }
    public string? DominantFishType { get; set; }
    public int DominantFishCount { get; set; }
    
    public string? FishCountsJson { get; set; }
    public string? DetectionsJson { get; set; }
    
    public DateTime AnalyzedAt { get; set; }
    
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
}
