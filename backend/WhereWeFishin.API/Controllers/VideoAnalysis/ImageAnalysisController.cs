using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageAnalysisController : ControllerBase
{
    private readonly IFishRecognitionService _fishRecognitionService;
    private readonly IRepository<ImageAnalysis> _imageRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImageAnalysisController> _logger;

    public ImageAnalysisController(
        IFishRecognitionService fishRecognitionService,
        IRepository<ImageAnalysis> imageRepository,
        IHttpClientFactory httpClientFactory,
        ILogger<ImageAnalysisController> logger)
    {
        _fishRecognitionService = fishRecognitionService;
        _imageRepository = imageRepository;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("analyze")]
    [Authorize]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<ActionResult<ImageAnalysisResultDto>> AnalyzeImage([FromForm] IFormFile image)
    {
        var userId = User.GetUserId();
        if (userId == null)
            return Unauthorized(new { error = "Invalid token" });

        if (image == null || image.Length == 0)
            return BadRequest(new { error = "No image file provided" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var fileExtension = Path.GetExtension(image.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(fileExtension))
            return BadRequest(new { error = "Invalid file type. Allowed: jpg, jpeg, png, webp" });

        if (image.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "File size exceeds 10MB limit" });

        try
        {
            await using var stream = image.OpenReadStream();
            var result = await _fishRecognitionService.AnalyzeImageAsync(stream, image.FileName, userId.Value);

            if (result.Success)
                return Ok(result);

            return StatusCode(500, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image");
            return StatusCode(500, new { error = "Failed to process image", details = ex.Message });
        }
    }

    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<ActionResult<List<ImageAnalysisSummaryDto>>> GetUserAnalyses(int userId)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized(new { error = "Invalid token" });

        if (currentUserId.Value != userId && !User.IsInRole("Admin"))
            return Forbid();

        var analyses = await _imageRepository.FindAsync(a => a.UserId == userId);
        var result = analyses
            .OrderByDescending(a => a.CreatedAt)
            .Select(MapToSummaryDto)
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<ImageAnalysisResultDto>> GetAnalysis(int id)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized(new { error = "Invalid token" });

        var analysis = await _imageRepository.GetByIdAsync(id);
        if (analysis == null)
            return NotFound(new { error = "Analysis not found" });

        if (analysis.UserId != currentUserId.Value && !User.IsInRole("Admin"))
            return Forbid();

        return Ok(MapToResultDto(analysis));
    }

    [HttpDelete("{id}")]
    [Authorize]
    public async Task<IActionResult> DeleteAnalysis(int id)
    {
        var currentUserId = User.GetUserId();
        if (currentUserId == null)
            return Unauthorized(new { error = "Invalid token" });

        var analysis = await _imageRepository.GetByIdAsync(id);
        if (analysis == null)
            return NotFound(new { error = "Analysis not found" });

        if (analysis.UserId != currentUserId.Value && !User.IsInRole("Admin"))
            return Forbid();

        if (!string.IsNullOrEmpty(analysis.ProcessedImageUrl))
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient("FishService");
                var filename = Path.GetFileName(analysis.ProcessedImageUrl);
                await httpClient.DeleteAsync($"api/delete-output/{filename}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Failed to delete processed image on Python service: {Error}", ex.Message);
            }
        }

        await _imageRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("processed-image/{*filename}")]
    public async Task<IActionResult> GetProcessedImage(string filename)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient("FishService");
            var response = await httpClient.GetAsync($"outputs/{filename}", HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
                return NotFound(new { error = "Image not found" });

            var contentType = response.Content.Headers.ContentType?.ToString() ?? "image/jpeg";
            var stream = await response.Content.ReadAsStreamAsync();
            return File(stream, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying processed image");
            return StatusCode(500, new { error = "Failed to retrieve image" });
        }
    }

    private static List<ImageDetectionDto>? DeserializeDetections(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<ImageDetectionDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static ImageAnalysisSummaryDto MapToSummaryDto(ImageAnalysis entity)
    {
        var detections = DeserializeDetections(entity.DetectionsJson);
        return new ImageAnalysisSummaryDto
        {
            Id = entity.Id,
            UserId = entity.UserId,
            FileName = entity.FileName,
            ProcessedImageUrl = entity.ProcessedImageUrl,
            TotalDetections = entity.TotalDetections,
            DominantFishType = entity.DominantFishType,
            DominantConfidence = entity.DominantConfidence,
            Detections = detections,
            AnalyzedAt = entity.AnalyzedAt,
            CreatedAt = entity.CreatedAt
        };
    }

    private static ImageAnalysisResultDto MapToResultDto(ImageAnalysis entity)
    {
        var detections = DeserializeDetections(entity.DetectionsJson);
        return new ImageAnalysisResultDto
        {
            Success = true,
            Id = entity.Id,
            UserId = entity.UserId,
            FileName = entity.FileName,
            Detections = detections,
            DominantDetection = detections?.FirstOrDefault(),
            ProcessedImageUrl = entity.ProcessedImageUrl,
            TotalDetections = entity.TotalDetections,
            AnalyzedAt = entity.AnalyzedAt,
            CreatedAt = entity.CreatedAt
        };
    }
}

