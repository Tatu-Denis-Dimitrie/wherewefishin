using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class VideoAnalysisControllerTests
{
    private readonly IFishRecognitionService _fishRecognitionService;
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly ILogger<VideoAnalysisController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly VideoAnalysisController _controller;
    private readonly List<VideoAnalysis> _analyses;

    public VideoAnalysisControllerTests()
    {
        _fishRecognitionService = Substitute.For<IFishRecognitionService>();
        _videoRepository = Substitute.For<IRepository<VideoAnalysis>>();
        _logger = Substitute.For<ILogger<VideoAnalysisController>>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();
        _analyses = _videoRepository.UseInMemoryStore<VideoAnalysis>();

        _controller = new VideoAnalysisController(
            _fishRecognitionService,
            _videoRepository,
            _logger,
            _httpClientFactory);

        SetUser(1);
        _controller.ControllerContext.HttpContext.Request.Scheme = "https";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("wherewefishin.test");
        _controller.ControllerContext.HttpContext.Response.Body = new MemoryStream();
    }

    private void SetUser(int userId, string role = Roles.User)
    {
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId, role);
        _controller.ControllerContext.HttpContext.Request.Scheme = "https";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("wherewefishin.test");
        _controller.ControllerContext.HttpContext.Response.Body = new MemoryStream();
    }

    private static VideoAnalysis CreateAnalysis(int id, int userId = 1, string status = nameof(AnalysisStatus.Completed)) => new()
    {
        Id = id,
        UserId = userId,
        FileName = $"analysis-{id}.mp4",
        VideoUrl = $"uploads/analysis-{id}.mp4",
        ProcessedVideoUrl = $"outputs/processed-{id}.mp4",
        Duration = 12.5,
        TotalFrames = 300,
        Fps = 24,
        TotalDetections = 4,
        DominantFishType = "Carp",
        DominantFishCount = 3,
        FishCountsJson = "{\"Carp\":3,\"Pike\":1}",
        DetectionsJson = "[{\"fishType\":\"Carp\",\"confidence\":0.98,\"timestamp\":1.2,\"frameNumber\":30,\"trackId\":4,\"bbox\":{\"x\":1,\"y\":2,\"width\":3,\"height\":4}}]",
        AnalyzedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        Status = Enum.Parse<AnalysisStatus>(status)
    };

    private static IFormFile CreateFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "video", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "video/mp4"
        };
    }

    [Fact]
    public async Task UploadVideo_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.UploadVideo(CreateFormFile("clip.mp4", [1, 2, 3]));

        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadVideo_WhenFileIsEmpty_ReturnsBadRequest()
    {
        // Arrange
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(0);
        file.FileName.Returns("clip.mp4");

        // Act
        var result = await _controller.UploadVideo(file);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadVideo_WhenExtensionIsInvalid_ReturnsBadRequest()
    {
        // Arrange
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(100);
        file.FileName.Returns("clip.txt");

        // Act
        var result = await _controller.UploadVideo(file);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadVideo_WhenFileIsTooLarge_ReturnsBadRequest()
    {
        // Arrange
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(151L * 1024 * 1024);
        file.FileName.Returns("clip.mp4");

        // Act
        var result = await _controller.UploadVideo(file);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task UploadVideo_WhenServiceSucceeds_ReturnsOk()
    {
        // Arrange
        var formFile = CreateFormFile("clip.mp4", [1, 2, 3, 4]);
        _fishRecognitionService.AnalyzeVideoAsync(Arg.Any<Stream>(), Arg.Any<string>(), 1)
            .Returns(new AnalysisResultDto
            {
                Success = true,
                Analysis = new VideoAnalysisDto { Id = 1, FileName = "clip.mp4", Status = "Completed" }
            });

        // Act
        var result = await _controller.UploadVideo(formFile);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<AnalysisResultDto>(okResult.Value);
        Assert.True(payload.Success);
        await _fishRecognitionService.Received(1).AnalyzeVideoAsync(
            Arg.Any<Stream>(),
            Arg.Is<string>(fileName => fileName.EndsWith("_clip.mp4", StringComparison.Ordinal)),
            1);
    }

    [Fact]
    public async Task UploadVideo_WhenServiceReturnsFailure_ReturnsServerError()
    {
        // Arrange
        var formFile = CreateFormFile("clip.mp4", [1, 2, 3, 4]);
        _fishRecognitionService.AnalyzeVideoAsync(Arg.Any<Stream>(), Arg.Any<string>(), 1)
            .Returns(new AnalysisResultDto { Success = false, Error = "Processing failed" });

        // Act
        var result = await _controller.UploadVideo(formFile);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetUserAnalyses_WhenAnonymous_ReturnsUnauthorized()
    {
        // Arrange
        ControllerContextFactory.SetAnonymousUser(_controller);

        // Act
        var result = await _controller.GetUserAnalyses(1);

        // Assert
        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task GetUserAnalyses_WhenForbidden_ReturnsForbid()
    {
        // Arrange
        SetUser(2);

        // Act
        var result = await _controller.GetUserAnalyses(1);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetUserAnalyses_ReturnsSortedSummariesWithAbsoluteVideoUrls()
    {
        // Arrange
        var older = CreateAnalysis(1, userId: 1);
        older.CreatedAt = DateTime.UtcNow.AddHours(-2);
        var newer = CreateAnalysis(2, userId: 1);
        newer.CreatedAt = DateTime.UtcNow.AddHours(-1);
        _analyses.AddRange([older, newer]);

        // Act
        var result = await _controller.GetUserAnalyses(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<VideoAnalysisSummaryDto>>(okResult.Value);
        Assert.Equal(2, payload.TotalItems);
        Assert.Equal(1, payload.Page);
        Assert.Equal(10, payload.PageSize);
        Assert.Equal(2, payload.Items.Count);
        Assert.Equal(2, payload.Items[0].Id);
        Assert.StartsWith("https://wherewefishin.test/uploads/analysis-2.mp4", payload.Items[0].VideoUrl);
    }

    [Fact]
    public async Task GetUserAnalyses_AppliesRequestedPagination()
    {
        // Arrange
        for (var index = 1; index <= 12; index++)
        {
            var analysis = CreateAnalysis(index, userId: 1);
            analysis.CreatedAt = DateTime.UtcNow.AddMinutes(index);
            _analyses.Add(analysis);
        }

        // Act
        var result = await _controller.GetUserAnalyses(1, page: 2, pageSize: 5);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PagedResponseDto<VideoAnalysisSummaryDto>>(okResult.Value);
        Assert.Equal(2, payload.Page);
        Assert.Equal(5, payload.PageSize);
        Assert.Equal(12, payload.TotalItems);
        Assert.Equal(3, payload.TotalPages);
        Assert.True(payload.HasPreviousPage);
        Assert.True(payload.HasNextPage);
        Assert.Equal([7, 6, 5, 4, 3], payload.Items.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetUserAnalysesOverview_ReturnsCountsAndMostRecentAnalyses()
    {
        var completedOld = CreateAnalysis(1, userId: 1, status: nameof(AnalysisStatus.Completed));
        completedOld.CreatedAt = DateTime.UtcNow.AddHours(-3);
        var failedRecent = CreateAnalysis(2, userId: 1, status: nameof(AnalysisStatus.Failed));
        failedRecent.CreatedAt = DateTime.UtcNow.AddHours(-2);
        var completedRecent = CreateAnalysis(3, userId: 1, status: nameof(AnalysisStatus.Completed));
        completedRecent.CreatedAt = DateTime.UtcNow.AddHours(-1);
        var completedNewest = CreateAnalysis(4, userId: 1, status: nameof(AnalysisStatus.Completed));
        completedNewest.CreatedAt = DateTime.UtcNow;
        _analyses.AddRange([completedOld, failedRecent, completedRecent, completedNewest]);

        var result = await _controller.GetUserAnalysesOverview(1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<VideoAnalysisOverviewDto>(okResult.Value);
        Assert.Equal(4, payload.TotalItems);
        Assert.Equal(3, payload.CompletedItems);
        Assert.Equal([4, 3, 2], payload.RecentAnalyses.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task GetAnalysis_WhenMissing_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetAnalysis(404);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAnalysis_WhenForbidden_ReturnsForbid()
    {
        // Arrange
        _analyses.Add(CreateAnalysis(1, userId: 3));

        // Act
        var result = await _controller.GetAnalysis(1);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetAnalysis_ReturnsMappedAnalysis()
    {
        // Arrange
        _analyses.Add(CreateAnalysis(1, userId: 1));

        // Act
        var result = await _controller.GetAnalysis(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var analysis = Assert.IsType<VideoAnalysisDto>(okResult.Value);
        Assert.Equal(1, analysis.Id);
        Assert.Equal(2, analysis.FishCounts!.Count);
        Assert.Single(analysis.Detections!);
        Assert.StartsWith("https://wherewefishin.test/uploads/analysis-1.mp4", analysis.VideoUrl);
    }

    [Fact]
    public async Task DeleteAnalysis_WhenForbidden_ReturnsForbid()
    {
        // Arrange
        _analyses.Add(CreateAnalysis(1, userId: 3));

        // Act
        var result = await _controller.DeleteAnalysis(1);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteAnalysis_WhenOwner_DeletesRepositoryEntry()
    {
        // Arrange
        var analysis = CreateAnalysis(1, userId: 1);
        analysis.VideoUrl = string.Empty;
        analysis.ProcessedVideoUrl = string.Empty;
        _analyses.Add(analysis);

        // Act
        var result = await _controller.DeleteAnalysis(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        Assert.True(_analyses.Single(current => current.Id == 1).IsDeleted);
    }

    [Fact]
    public async Task CheckServiceHealth_WhenHealthy_ReturnsOk()
    {
        // Arrange
        _fishRecognitionService.IsServiceHealthyAsync().Returns(true);

        // Act
        var result = await _controller.CheckServiceHealth();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task CheckServiceHealth_WhenUnhealthy_ReturnsServiceUnavailable()
    {
        // Arrange
        _fishRecognitionService.IsServiceHealthyAsync().Returns(false);

        // Act
        var result = await _controller.CheckServiceHealth();

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetSupportedFish_ReturnsFishTypesAndCount()
    {
        // Arrange
        _fishRecognitionService.GetSupportedFishTypesAsync().Returns(["Carp", "Pike", "Catfish"]);

        // Act
        var result = await _controller.GetSupportedFish();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = okResult.Value!;
        Assert.Equal(3, value.GetType().GetProperty("total")!.GetValue(value));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}