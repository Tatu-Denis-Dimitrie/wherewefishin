namespace WhereWeFishin.Core.DTOs;

public class VideoAnalysisDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? ProcessedVideoUrl { get; set; }
    public double Duration { get; set; }
    public int TotalFrames { get; set; }
    public int Fps { get; set; }
    public int TotalDetections { get; set; }
    public string? DominantFishType { get; set; }
    public int DominantFishCount { get; set; }
    public Dictionary<string, int>? FishCounts { get; set; }
    public List<FishDetectionDto>? Detections { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FishDetectionDto
{
    public string FishType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public double Timestamp { get; set; }
    public int FrameNumber { get; set; }
    public BoundingBoxDto BBox { get; set; } = new();
}

public class BoundingBoxDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class UploadVideoRequest
{
    public string? Description { get; set; }
}

public class AnalysisResultDto
{
    public bool Success { get; set; }
    public VideoAnalysisDto? Analysis { get; set; }
    public string? Error { get; set; }
}
