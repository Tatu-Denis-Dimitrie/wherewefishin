using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.Core.Services;

public class FishRecognitionService : IFishRecognitionService
{
    private readonly HttpClient _httpClient;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly IRepository<ImageAnalysis> _imageRepository;
    private readonly string _pythonServiceUrl;
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FishRecognitionService(
        HttpClient httpClient,
        IRepository<VideoAnalysis> videoRepository,
        IRepository<ImageAnalysis> imageRepository,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _videoRepository = videoRepository;
        _imageRepository = imageRepository;
        _pythonServiceUrl = configuration["FishRecognitionService:Url"] ?? "http://localhost:5001";
    }

    public async Task<AnalysisResultDto> AnalyzeVideoAsync(Stream videoStream, string fileName, int userId)
    {
        var analysis = new VideoAnalysis
        {
            UserId = userId,
            FileName = fileName,
            VideoUrl = $"uploads/{fileName}",
            Status = AnalysisStatus.Processing,
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
                return await FailAnalysis(analysis, $"Python service error: {error}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var pythonResult = JsonSerializer.Deserialize<PythonAnalysisResponse>(responseContent, _jsonOptions);

            if (pythonResult?.Success != true || pythonResult.Results == null)
                return await FailAnalysis(analysis, "Invalid response from Python service");

            var results = pythonResult.Results;
            analysis.Duration = results.Duration;
            analysis.TotalFrames = results.TotalFrames;
            analysis.Fps = results.Fps;
            analysis.TotalDetections = results.TotalUniqueFish ?? results.TotalDetections;
            analysis.ProcessedVideoUrl = results.ProcessedVideoUrl;

            if (results.DominantFish != null)
            {
                analysis.DominantFishType = results.DominantFish.Type;
                analysis.DominantFishCount = results.DominantFish.Count;
            }

            analysis.FishCountsJson = JsonSerializer.Serialize(results.FishCounts);
            analysis.DetectionsJson = JsonSerializer.Serialize(results.Detections);
            analysis.Status = AnalysisStatus.Completed;
            await _videoRepository.UpdateAsync(analysis);

            return new AnalysisResultDto { Success = true, Analysis = MapToDto(analysis, results) };
        }
        catch (Exception ex)
        {
            if (analysis.Id > 0)
                await FailAnalysis(analysis, ex.Message);

            return new AnalysisResultDto { Success = false, Error = ex.Message };
        }
    }

    private async Task<AnalysisResultDto> FailAnalysis(VideoAnalysis analysis, string error)
    {
        analysis.Status = AnalysisStatus.Failed;
        analysis.ErrorMessage = error;
        try { await _videoRepository.UpdateAsync(analysis); } catch { /* best-effort */ }
        return new AnalysisResultDto { Success = false, Error = error };
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
                var result = JsonSerializer.Deserialize<SupportedFishResponse>(content, _jsonOptions);
                return result?.FishTypes ?? new List<string>();
            }
        }
        catch
        {}

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
            Status = entity.Status.ToString(),
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

    // ── Image analysis ───────────────────────────────────────────────────

    public async Task<ImageAnalysisResultDto> AnalyzeImageAsync(Stream imageStream, string fileName, int userId)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(imageStream);
            var ext = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
            var contentType = ext switch
            {
                "png" => "image/png",
                "webp" => "image/webp",
                _ => "image/jpeg"
            };
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            content.Add(streamContent, "image", fileName);

            var response = await _httpClient.PostAsync($"{_pythonServiceUrl}/api/analyze-image", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return new ImageAnalysisResultDto { Success = false, Error = $"Python service error: {errorBody}" };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var pythonResult = JsonSerializer.Deserialize<PythonImageAnalysisResponse>(responseContent, _jsonOptions);

            if (pythonResult?.Success != true)
                return new ImageAnalysisResultDto { Success = false, Error = "Invalid response from Python service" };

            var detections = pythonResult.Detections?.Select(d => new ImageDetectionDto
            {
                FishType = d.FishType ?? string.Empty,
                Confidence = d.Confidence,
                BBox = new BoundingBoxDto
                {
                    X = d.BBox?.X ?? 0,
                    Y = d.BBox?.Y ?? 0,
                    Width = d.BBox?.Width ?? 0,
                    Height = d.BBox?.Height ?? 0
                },
                ClassProbabilities = d.ClassProbs?.Select(cp => new ClassProbabilityDto
                {
                    FishType = cp.FishType ?? string.Empty,
                    Confidence = cp.Confidence
                }).ToList()
            }).ToList();

            var dominant = detections?.FirstOrDefault();

            var analysis = new ImageAnalysis
            {
                UserId = userId,
                FileName = fileName,
                ProcessedImageUrl = pythonResult.ProcessedImageUrl,
                TotalDetections = pythonResult.TotalDetections,
                DominantFishType = dominant?.FishType,
                DominantConfidence = dominant?.Confidence ?? 0,
                DetectionsJson = JsonSerializer.Serialize(detections),
                AnalyzedAt = DateTime.UtcNow
            };

            await _imageRepository.AddAsync(analysis);

            return new ImageAnalysisResultDto
            {
                Success = true,
                Id = analysis.Id,
                UserId = analysis.UserId,
                FileName = analysis.FileName,
                Detections = detections,
                DominantDetection = dominant,
                ProcessedImageUrl = analysis.ProcessedImageUrl,
                TotalDetections = analysis.TotalDetections,
                AnalyzedAt = analysis.AnalyzedAt,
                CreatedAt = analysis.CreatedAt
            };
        }
        catch (Exception ex)
        {
            return new ImageAnalysisResultDto { Success = false, Error = ex.Message };
        }
    }

    private class PythonImageAnalysisResponse
    {
        public bool Success { get; set; }
        public List<PythonImageDetection>? Detections { get; set; }
        public PythonImageDetection? DominantDetection { get; set; }

        [JsonPropertyName("processedImageUrl")]
        public string? ProcessedImageUrl { get; set; }

        public int TotalDetections { get; set; }
        public string? Error { get; set; }
    }

    private class PythonImageDetection
    {
        public string? FishType { get; set; }
        public double Confidence { get; set; }
        public PythonBBox? BBox { get; set; }

        [JsonPropertyName("classProbs")]
        public List<PythonClassProbability>? ClassProbs { get; set; }
    }

    private class PythonClassProbability
    {
        public string? FishType { get; set; }
        public double Confidence { get; set; }
    }
}
