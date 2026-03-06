using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoAnalysisController : ControllerBase
{
    private readonly IFishRecognitionService _fishRecognitionService;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly ILogger<VideoAnalysisController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _fishRecognitionServiceUrl;

    public VideoAnalysisController(
        IFishRecognitionService fishRecognitionService,
        IRepository<VideoAnalysis> videoRepository,
        ILogger<VideoAnalysisController> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _fishRecognitionService = fishRecognitionService;
        _videoRepository = videoRepository;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _fishRecognitionServiceUrl = configuration["FishRecognitionService:Url"] ?? "http://localhost:5001";
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(150 * 1024 * 1024)] // 150MB
    [RequestFormLimits(MultipartBodyLengthLimit = 150 * 1024 * 1024)]
    public async Task<ActionResult<AnalysisResultDto>> UploadVideo([FromForm] IFormFile video)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { error = "Invalid token" });

        if (video == null || video.Length == 0)
        {
            return BadRequest(new { error = "No video file provided" });
        }

        var allowedExtensions = new[] { ".mp4", ".avi", ".mov", ".mkv" };
        var fileExtension = Path.GetExtension(video.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
        {
            return BadRequest(new { error = "Invalid file type. Allowed: mp4, avi, mov, mkv" });
        }

        if (video.Length > 150 * 1024 * 1024)
        {
            return BadRequest(new { error = "File size exceeds 150MB limit" });
        }

        try
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsPath))
            {
                Directory.CreateDirectory(uploadsPath);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{video.FileName}";
            var filePath = Path.Combine(uploadsPath, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await video.CopyToAsync(fileStream);
            }

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var result = await _fishRecognitionService.AnalyzeVideoAsync(stream, uniqueFileName, userId);

            try
            {
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete uploaded file: {FilePath}", filePath);
            }
            
            if (result.Success)
            {
                return Ok(result);
            }
            
            return StatusCode(500, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading and analyzing video");
            return StatusCode(500, new { error = "Failed to process video", details = ex.Message });
        }
    }

    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<VideoAnalysisDto>>> GetUserAnalyses(int userId)
    {
        var analyses = await _videoRepository.GetAllAsync();
        var userAnalyses = analyses
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapToDto);

        return Ok(userAnalyses);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<VideoAnalysisDto>> GetAnalysis(int id)
    {
        var analysis = await _videoRepository.GetByIdAsync(id);
        if (analysis == null)
        {
            return NotFound();
        }

        return Ok(MapToDto(analysis));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAnalysis(int id)
    {
        var analysis = await _videoRepository.GetByIdAsync(id);
        if (analysis == null)
            return NotFound();

        if (!string.IsNullOrEmpty(analysis.ProcessedVideoUrl))
        {
            try
            {
                var outputFilename = Path.GetFileName(analysis.ProcessedVideoUrl.Replace('/', Path.DirectorySeparatorChar));
                var httpClient = _httpClientFactory.CreateClient();
                await httpClient.DeleteAsync($"{_fishRecognitionServiceUrl}/api/delete-output/{outputFilename}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete processed video for analysis {Id}", id);
            }
        }

        if (!string.IsNullOrEmpty(analysis.VideoUrl))
        {
            try
            {
                var uploadFilename = Path.GetFileName(analysis.VideoUrl.Replace('/', Path.DirectorySeparatorChar));
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads", uploadFilename);
                if (System.IO.File.Exists(uploadPath))
                    System.IO.File.Delete(uploadPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete original video for analysis {Id}", id);
            }
        }

        await _videoRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("health")]
    public async Task<ActionResult> CheckServiceHealth()
    {
        var isHealthy = await _fishRecognitionService.IsServiceHealthyAsync();
        
        if (isHealthy)
        {
            return Ok(new { status = "healthy", service = "fish-recognition" });
        }

        return StatusCode(503, new { status = "unhealthy", service = "fish-recognition" });
    }

    [HttpGet("supported-fish")]
    public async Task<ActionResult<List<string>>> GetSupportedFish()
    {
        var fishTypes = await _fishRecognitionService.GetSupportedFishTypesAsync();
        return Ok(new { fishTypes, total = fishTypes.Count });
    }

    [HttpGet("processed-video/{*filename}")]
    public async Task<IActionResult> GetProcessedVideo(string filename)
    {
        try
        {
            var videoUrl = $"{_fishRecognitionServiceUrl}/outputs/{filename}";

            var httpClient = _httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, videoUrl);

            // Forward Range header so Python can return partial content (needed for video seeking)
            if (Request.Headers.TryGetValue("Range", out var rangeValues))
                requestMessage.Headers.TryAddWithoutValidation("Range", rangeValues.ToArray());

            // ResponseHeadersRead = stream body instead of loading entire video into memory
            var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                return StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "video/mp4";

            // Set status and headers before writing body
            Response.StatusCode = (int)response.StatusCode;
            Response.ContentType = contentType;
            Response.Headers["Accept-Ranges"] = "bytes";

            if (response.Content.Headers.ContentRange != null)
                Response.Headers["Content-Range"] = response.Content.Headers.ContentRange.ToString();

            if (response.Content.Headers.ContentLength.HasValue)
                Response.Headers["Content-Length"] = response.Content.Headers.ContentLength.Value.ToString();

            // Stream body directly to response without buffering
            await using var stream = await response.Content.ReadAsStreamAsync();
            await stream.CopyToAsync(Response.Body);
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying processed video: {Filename}", filename);
            return StatusCode(500, new { error = "Failed to retrieve processed video" });
        }
    }

    [HttpGet("test-uploads")]
    public IActionResult TestUploadsAccess()
    {
        try
        {
            var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            
            if (!Directory.Exists(uploadsPath))
            {
                return Ok(new { 
                    status = "error",
                    message = "Uploads directory does not exist",
                    path = uploadsPath 
                });
            }

            var files = Directory.GetFiles(uploadsPath)
                .Select(f => new {
                    filename = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    url = $"{Request.Scheme}://{Request.Host}/uploads/{Path.GetFileName(f)}"
                })
                .ToList();

            return Ok(new {
                status = "success",
                uploadsPath = uploadsPath,
                fileCount = files.Count,
                files = files,
                testUrl = files.Any() ? files[0].url : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing uploads access");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private VideoAnalysisDto MapToDto(VideoAnalysis entity)
    {
        Dictionary<string, int>? fishCounts = null;
        List<FishDetectionDto>? detections = null;

        if (!string.IsNullOrEmpty(entity.FishCountsJson))
        {
            try
            {
                fishCounts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(entity.FishCountsJson);
            }
            catch { }
        }

        if (!string.IsNullOrEmpty(entity.DetectionsJson))
        {
            try
            {
                detections = System.Text.Json.JsonSerializer.Deserialize<List<FishDetectionDto>>(entity.DetectionsJson);
            }
            catch { }
        }

        string videoUrl = entity.VideoUrl;
        if (!string.IsNullOrEmpty(videoUrl) && !videoUrl.StartsWith("http"))
        {
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            videoUrl = $"{baseUrl}/{videoUrl}";
        }

        string? processedVideoUrl = entity.ProcessedVideoUrl;
        // Return the relative path as-is (e.g. "outputs/filename.mp4").
        // The frontend routes it to the Python service directly (local)
        // or via nginx /outputs/ proxy (Docker). No .NET proxying needed.

        return new VideoAnalysisDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FileName = entity.FileName,
            VideoUrl = videoUrl,
            ProcessedVideoUrl = processedVideoUrl,
            Duration = entity.Duration,
            TotalFrames = entity.TotalFrames,
            Fps = entity.Fps,
            TotalDetections = entity.TotalDetections,
            DominantFishType = entity.DominantFishType,
            DominantFishCount = entity.DominantFishCount,
            FishCounts = fishCounts,
            Detections = detections,
            AnalyzedAt = entity.AnalyzedAt,
            Status = entity.Status,
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt
        };
    }
}
