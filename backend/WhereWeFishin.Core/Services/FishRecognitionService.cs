using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Core.Services;

public class FishRecognitionService : IFishRecognitionService
{
    private readonly HttpClient _httpClient;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly string _pythonServiceUrl;

    public FishRecognitionService(
        HttpClient httpClient,
        IRepository<VideoAnalysis> videoRepository,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _videoRepository = videoRepository;
        _pythonServiceUrl = configuration["FishRecognitionService:Url"] ?? "http://localhost:5001";
    }

    public async Task<AnalysisResultDto> AnalyzeVideoAsync(Stream videoStream, string fileName, int userId)
    {
        VideoAnalysis analysis = new()
        {
            UserId = userId,
            FileName = fileName,
            VideoUrl = $"uploads/{fileName}",
            Status = "Processing",
            AnalyzedAt = DateTime.UtcNow
        };

        try
        {
            await _videoRepository.AddAsync(analysis);

            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(videoStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
            content.Add(streamContent, "video", fileName);

            var response = await _httpClient.PostAsync($"{_pythonServiceUrl}/api/analyze-video", content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                analysis.Status = "Failed";
                analysis.ErrorMessage = $"Python service error: {error}";
                await _videoRepository.UpdateAsync(analysis);

                return new AnalysisResultDto
                {
                    Success = false,
                    Error = analysis.ErrorMessage
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var pythonResult = JsonSerializer.Deserialize<PythonAnalysisResponse>(responseContent, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (pythonResult?.Success == true && pythonResult.Results != null)
            {
                analysis.Duration = pythonResult.Results.Duration;
                analysis.TotalFrames = pythonResult.Results.TotalFrames;
                analysis.Fps = pythonResult.Results.Fps;
                analysis.TotalDetections = pythonResult.Results.TotalUniqueFish ?? pythonResult.Results.TotalDetections;
                
                if (!string.IsNullOrEmpty(pythonResult.Results.ProcessedVideoUrl))
                {
                    analysis.ProcessedVideoUrl = pythonResult.Results.ProcessedVideoUrl;
                }
                
                if (pythonResult.Results.DominantFish != null)
                {
                    analysis.DominantFishType = pythonResult.Results.DominantFish.Type;
                    analysis.DominantFishCount = pythonResult.Results.DominantFish.Count;
                }

                analysis.FishCountsJson = JsonSerializer.Serialize(pythonResult.Results.FishCounts);
                analysis.DetectionsJson = JsonSerializer.Serialize(pythonResult.Results.Detections);
                analysis.Status = "Completed";

                await _videoRepository.UpdateAsync(analysis);

                return new AnalysisResultDto
                {
                    Success = true,
                    Analysis = MapToDto(analysis, pythonResult.Results)
                };
            }

            analysis.Status = "Failed";
            analysis.ErrorMessage = "Invalid response from Python service";
            await _videoRepository.UpdateAsync(analysis);

            return new AnalysisResultDto
            {
                Success = false,
                Error = analysis.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            analysis.Status = "Failed";
            analysis.ErrorMessage = ex.Message;

            // Only update if the row was actually persisted (AddAsync succeeded)
            if (analysis.Id > 0)
            {
                try { await _videoRepository.UpdateAsync(analysis); } catch { /* best-effort */ }
            }

            return new AnalysisResultDto
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<bool> IsServiceHealthyAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_pythonServiceUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetSupportedFishTypesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_pythonServiceUrl}/api/supported-fish");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SupportedFishResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return result?.FishTypes ?? new List<string>();
            }
        }
        catch
        {
            // Log error
        }

        return new List<string>();
    }

    private VideoAnalysisDto MapToDto(VideoAnalysis entity, PythonResults results)
    {
        var detections = results.Detections?.Select(d => new FishDetectionDto
        {
            FishType = d.FishType ?? string.Empty,
            Confidence = d.Confidence,
            Timestamp = d.Timestamp,
            FrameNumber = d.FrameNumber,
            TrackId = d.TrackId,
            BBox = new BoundingBoxDto
            {
                X = d.BBox?.X ?? 0,
                Y = d.BBox?.Y ?? 0,
                Width = d.BBox?.Width ?? 0,
                Height = d.BBox?.Height ?? 0
            }
        }).ToList();

        string? processedVideoUrl = entity.ProcessedVideoUrl;
        // Keep as relative path (e.g. "outputs/filename.mp4") — the frontend resolves it.

        return new VideoAnalysisDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FileName = entity.FileName,
            VideoUrl = entity.VideoUrl,
            ProcessedVideoUrl = processedVideoUrl,
            Duration = entity.Duration,
            TotalFrames = entity.TotalFrames,
            Fps = entity.Fps,
            TotalDetections = entity.TotalDetections,
            TotalUniqueFish = results.TotalUniqueFish ?? entity.TotalDetections,
            DominantFishType = entity.DominantFishType,
            DominantFishCount = entity.DominantFishCount,
            FishCounts = results.FishCounts,
            Detections = detections,
            AnalyzedAt = entity.AnalyzedAt,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt
        };
    }

    private class PythonAnalysisResponse
    {
        public bool Success { get; set; }
        public PythonResults? Results { get; set; }
        public string? Error { get; set; }
    }

    private class PythonResults
    {
        public int TotalFrames { get; set; }
        public double Duration { get; set; }
        public int Fps { get; set; }
        public List<PythonDetection>? Detections { get; set; }
        public Dictionary<string, int>? FishCounts { get; set; }
        public PythonDominantFish? DominantFish { get; set; }
        public int TotalDetections { get; set; }
        public int? TotalUniqueFish { get; set; }
        public int? TotalFrameDetections { get; set; }
        
        [JsonPropertyName("processed_video_url")]
        public string? ProcessedVideoUrl { get; set; }
    }

    private class PythonDetection
    {
        public string? FishType { get; set; }
        public double Confidence { get; set; }
        public double Timestamp { get; set; }
        public int FrameNumber { get; set; }
        public int? TrackId { get; set; }
        public PythonBBox? BBox { get; set; }
    }

    private class PythonBBox
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    private class PythonDominantFish
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private class SupportedFishResponse
    {
        public List<string>? FishTypes { get; set; }
        public int Total { get; set; }
    }
}
