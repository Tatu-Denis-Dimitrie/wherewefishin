using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VideoAnalysisController : ControllerBase
{
    private readonly IFishRecognitionService _fishRecognitionService;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly ILogger<VideoAnalysisController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApplicationDbContext _context;

    public VideoAnalysisController(
        IFishRecognitionService fishRecognitionService,
        IRepository<VideoAnalysis> videoRepository,
        ILogger<VideoAnalysisController> logger,
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext context)
    {
        _fishRecognitionService = fishRecognitionService;
        _videoRepository = videoRepository;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _context = context;
    }

    [HttpPost("upload")]
    [Authorize]
    [RequestSizeLimit(150 * 1024 * 1024)] // 150MB
    [RequestFormLimits(MultipartBodyLengthLimit = 150 * 1024 * 1024)]
    public async Task<ActionResult<AnalysisResultDto>> UploadVideo([FromForm] IFormFile video)
    {
        var accessError = EnsureRecognitionAccess();
        if (accessError != null)
            return accessError;

        var userId = User.GetUserId();
        if (userId == null)
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
            var result = await _fishRecognitionService.AnalyzeVideoAsync(stream, uniqueFileName, userId.Value);

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
    [Authorize]
    public async Task<ActionResult<PagedResponseDto<VideoAnalysisSummaryDto>>> GetUserAnalyses(
        int userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var recognitionAccessError = EnsureRecognitionAccess();
        if (recognitionAccessError != null)
            return recognitionAccessError;

        var accessError = ValidateUserAnalysesAccess(userId);
        if (accessError != null)
            return accessError;

        page = Math.Max(page, 1);
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 50);

        var baseUrl = GetRequestBaseUrl();
        var analysesQuery = _context.VideoAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.UserId == userId);

        var totalItems = await analysesQuery.CountAsync();
        var pageItems = await analysesQuery
            .OrderByDescending(analysis => analysis.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(analysis => new
            {
                analysis.Id,
                analysis.UserId,
                analysis.FileName,
                analysis.VideoUrl,
                analysis.ProcessedVideoUrl,
                analysis.Duration,
                analysis.TotalDetections,
                analysis.DominantFishType,
                analysis.DominantFishCount,
                analysis.AnalyzedAt,
                analysis.Status,
                analysis.ErrorMessage,
                analysis.CreatedAt
            })
            .ToListAsync();

        var items = pageItems
            .Select(item => NormalizeSummaryVideoUrls(new VideoAnalysisSummaryDto
            {
                Id = item.Id,
                UserId = item.UserId,
                FileName = item.FileName,
                VideoUrl = item.VideoUrl,
                ProcessedVideoUrl = item.ProcessedVideoUrl,
                Duration = item.Duration,
                TotalDetections = item.TotalDetections,
                TotalUniqueFish = item.TotalDetections,
                DominantFishType = item.DominantFishType,
                DominantFishCount = item.DominantFishCount,
                AnalyzedAt = item.AnalyzedAt,
                Status = item.Status.ToString(),
                ErrorMessage = item.ErrorMessage,
                CreatedAt = item.CreatedAt
            }, baseUrl))
            .ToList();

        return Ok(new PagedResponseDto<VideoAnalysisSummaryDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems
        });
    }

    [HttpGet("user/{userId}/overview")]
    [Authorize]
    public async Task<ActionResult<VideoAnalysisOverviewDto>> GetUserAnalysesOverview(int userId)
    {
        var recognitionAccessError = EnsureRecognitionAccess();
        if (recognitionAccessError != null)
            return recognitionAccessError;

        var accessError = ValidateUserAnalysesAccess(userId);
        if (accessError != null)
            return accessError;

        var overviewStats = await _context.VideoAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalItems = group.Count(),
                CompletedItems = group.Count(analysis => analysis.Status == AnalysisStatus.Completed)
            })
            .FirstOrDefaultAsync();

        var recentAnalyses = await _context.VideoAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(3)
            .ToListAsync();

        return Ok(new VideoAnalysisOverviewDto
        {
            TotalItems = overviewStats?.TotalItems ?? 0,
            CompletedItems = overviewStats?.CompletedItems ?? 0,
            RecentAnalyses = recentAnalyses
                .Select(MapToSummaryDto)
                .ToList()
        });
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<VideoAnalysisDto>> GetAnalysis(int id)
    {
        var accessError = EnsureRecognitionAccess();
        if (accessError != null)
            return accessError;

        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized();

        var analysis = await _videoRepository.GetByIdAsync(id);
        if (analysis == null)
        {
            return NotFound();
        }

        if (analysis.UserId != currentUserId.Value && !User.IsInRole(Roles.Admin))
            return Forbid();

        return Ok(MapToDto(analysis));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteAnalysis(int id)
    {
        var accessError = EnsureRecognitionAccess();
        if (accessError != null)
            return accessError;

        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized();

        var analysis = await _videoRepository.GetByIdAsync(id);
        if (analysis == null)
            return NotFound();

        if (analysis.UserId != currentUserId.Value && !User.IsInRole(Roles.Admin))
            return Forbid();

        if (!string.IsNullOrEmpty(analysis.ProcessedVideoUrl))
        {
            try
            {
                var outputFilename = Path.GetFileName(analysis.ProcessedVideoUrl.Replace('/', Path.DirectorySeparatorChar));
                var httpClient = _httpClientFactory.CreateClient("FishService");
                await httpClient.DeleteAsync($"api/delete-output/{outputFilename}");
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
    [OutputCache(PolicyName = "LongCache")]
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
            var httpClient = _httpClientFactory.CreateClient("FishService");
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"outputs/{filename}");

            if (Request.Headers.TryGetValue("Range", out var rangeValues))
                requestMessage.Headers.TryAddWithoutValidation("Range", rangeValues.ToArray());

            var response = await httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return NotFound();

            if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.PartialContent)
                return StatusCode((int)response.StatusCode);

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "video/mp4";

            Response.StatusCode = (int)response.StatusCode;
            Response.ContentType = contentType;
            Response.Headers["Accept-Ranges"] = "bytes";

            if (response.Content.Headers.ContentRange != null)
                Response.Headers["Content-Range"] = response.Content.Headers.ContentRange.ToString();

            if (response.Content.Headers.ContentLength.HasValue)
                Response.Headers["Content-Length"] = response.Content.Headers.ContentLength.Value.ToString();

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

    private VideoAnalysisDto MapToDto(VideoAnalysis entity, bool includeDetections = true)
    {
        Dictionary<string, int>? fishCounts = null;
        List<FishDetectionDto>? detections = null;

        if (!string.IsNullOrEmpty(entity.FishCountsJson))
        {
            try
            {
                fishCounts = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(entity.FishCountsJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize FishCountsJson for analysis {AnalysisId}", entity.Id);
            }
        }

        if (includeDetections && !string.IsNullOrEmpty(entity.DetectionsJson))
        {
            try
            {
                detections = System.Text.Json.JsonSerializer.Deserialize<List<FishDetectionDto>>(entity.DetectionsJson);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize DetectionsJson for analysis {AnalysisId}", entity.Id);
            }
        }

        string videoUrl = entity.VideoUrl;
        if (!string.IsNullOrEmpty(videoUrl) && !videoUrl.StartsWith("http"))
        {
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            videoUrl = $"{baseUrl}/{videoUrl}";
        }

        string? processedVideoUrl = entity.ProcessedVideoUrl;

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
            TotalUniqueFish = entity.TotalDetections,
            DominantFishType = entity.DominantFishType,
            DominantFishCount = entity.DominantFishCount,
            FishCounts = fishCounts,
            Detections = detections,
            AnalyzedAt = entity.AnalyzedAt,
            Status = entity.Status.ToString(),
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt
        };
    }

    private VideoAnalysisSummaryDto MapToSummaryDto(VideoAnalysis entity)
    {
        return new VideoAnalysisSummaryDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FileName = entity.FileName,
            VideoUrl = entity.VideoUrl,
            ProcessedVideoUrl = entity.ProcessedVideoUrl,
            Duration = entity.Duration,
            TotalDetections = entity.TotalDetections,
            TotalUniqueFish = entity.TotalDetections,
            DominantFishType = entity.DominantFishType,
            DominantFishCount = entity.DominantFishCount,
            AnalyzedAt = entity.AnalyzedAt,
            Status = entity.Status.ToString(),
            ErrorMessage = entity.ErrorMessage,
            CreatedAt = entity.CreatedAt
        };
    }

    private VideoAnalysisSummaryDto NormalizeSummaryVideoUrls(VideoAnalysisSummaryDto item, string baseUrl)
    {
        if (!string.IsNullOrEmpty(item.VideoUrl) && !item.VideoUrl.StartsWith("http"))
        {
            item.VideoUrl = $"{baseUrl}/{item.VideoUrl}";
        }

        return item;
    }

    private string GetRequestBaseUrl()
    {
        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }

    private ActionResult? ValidateUserAnalysesAccess(int userId)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized();

        if (currentUserId.Value != userId && !User.IsInRole(Roles.Admin))
            return Forbid();

        return null;
    }

    private ActionResult? EnsureRecognitionAccess()
    {
        if (User.IsInRole(Roles.Employee))
            return Forbid();

        return null;
    }
}
