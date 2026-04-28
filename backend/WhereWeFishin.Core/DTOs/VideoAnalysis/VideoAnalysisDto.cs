namespace WhereWeFishin.Core.DTOs;

public class PagedResponseDto<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => TotalItems == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => TotalPages > 0 && Page < TotalPages;
}

public class VideoAnalysisSummaryDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public string? ProcessedVideoUrl { get; set; }
    public double Duration { get; set; }
    public int TotalDetections { get; set; }
    public int? TotalUniqueFish { get; set; }
    public string? DominantFishType { get; set; }
    public int DominantFishCount { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class VideoAnalysisOverviewDto
{
    public int TotalItems { get; set; }
    public int CompletedItems { get; set; }
    public IReadOnlyList<VideoAnalysisSummaryDto> RecentAnalyses { get; set; } = [];
}

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
    public int? TotalUniqueFish { get; set; }
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
    public int? TrackId { get; set; }
    public BoundingBoxDto BBox { get; set; } = new();
}

public class BoundingBoxDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public class AnalysisResultDto
{
    public bool Success { get; set; }
    public VideoAnalysisDto? Analysis { get; set; }
    public string? Error { get; set; }
}

public class ClassProbabilityDto
{
    public string FishType { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public class ImageDetectionDto
{
    public string FishType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public BoundingBoxDto BBox { get; set; } = new();
    public List<ClassProbabilityDto>? ClassProbabilities { get; set; }
}

public class ImageAnalysisResultDto
{
    public bool Success { get; set; }
    public int? Id { get; set; }
    public int? UserId { get; set; }
    public string? FileName { get; set; }
    public List<ImageDetectionDto>? Detections { get; set; }
    public ImageDetectionDto? DominantDetection { get; set; }
    public string? ProcessedImageUrl { get; set; }
    public int TotalDetections { get; set; }
    public DateTime? AnalyzedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? Error { get; set; }
}

public class ImageAnalysisSummaryDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ProcessedImageUrl { get; set; }
    public int TotalDetections { get; set; }
    public string? DominantFishType { get; set; }
    public double DominantConfidence { get; set; }
    public List<ImageDetectionDto>? Detections { get; set; }
    public DateTime AnalyzedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
