using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Core.Services;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Services;

public class FishRecognitionServiceTests
{
    private readonly IRepository<VideoAnalysis> _videoRepository;
    private readonly List<VideoAnalysis> _analyses;

    public FishRecognitionServiceTests()
    {
        _videoRepository = Substitute.For<IRepository<VideoAnalysis>>();
        _analyses = _videoRepository.UseInMemoryStore<VideoAnalysis>();
    }

    [Fact]
    public async Task AnalyzeVideoAsync_WhenPythonServiceSucceeds_ReturnsMappedAnalysisAndPersistsCompletedState()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """
                {
                  "success": true,
                  "results": {
                    "duration": 12.5,
                    "totalFrames": 250,
                    "fps": 25,
                    "totalDetections": 12,
                    "totalUniqueFish": 2,
                    "processed_video_url": "outputs/processed.mp4",
                    "fishCounts": { "Carp": 2, "Pike": 1 },
                    "dominantFish": { "type": "Carp", "count": 2 },
                    "detections": [
                      {
                        "fishType": "Carp",
                        "confidence": 0.95,
                        "timestamp": 1.5,
                        "frameNumber": 37,
                        "trackId": 10,
                        "bBox": { "x": 1, "y": 2, "width": 3, "height": 4 }
                      }
                    ]
                  }
                }
                """,
                Encoding.UTF8,
                "application/json")
        }));

        var service = CreateService(handler, "http://fish.test");

        await using var stream = new MemoryStream([1, 2, 3, 4]);
        var result = await service.AnalyzeVideoAsync(stream, "clip.mp4", 7);

        Assert.True(result.Success);
        Assert.NotNull(result.Analysis);
        Assert.Equal("clip.mp4", result.Analysis!.FileName);
        Assert.Equal(250, result.Analysis.TotalFrames);
        Assert.Equal(25, result.Analysis.Fps);
        Assert.Equal(2, result.Analysis.TotalDetections);
        Assert.Equal(2, result.Analysis.TotalUniqueFish);
        Assert.Equal("Carp", result.Analysis.DominantFishType);
        Assert.Equal(2, result.Analysis.DominantFishCount);
        Assert.Equal("outputs/processed.mp4", result.Analysis.ProcessedVideoUrl);
        Assert.NotNull(result.Analysis.Detections);
        Assert.Single(result.Analysis.Detections!);

        var analysis = Assert.Single(_analyses);
        Assert.Equal(AnalysisStatus.Completed, analysis.Status);
        Assert.Equal(7, analysis.UserId);
        Assert.Equal("outputs/processed.mp4", analysis.ProcessedVideoUrl);
        Assert.Equal(2, analysis.TotalDetections);
        Assert.Equal("Carp", analysis.DominantFishType);
        Assert.NotNull(analysis.FishCountsJson);
        Assert.Equal(2, JsonSerializer.Deserialize<Dictionary<string, int>>(analysis.FishCountsJson!)!["Carp"]);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("http://fish.test/api/analyze-video", handler.LastRequest.RequestUri!.ToString());
        Assert.IsType<MultipartFormDataContent>(handler.LastRequest.Content);
    }

    [Fact]
    public async Task AnalyzeVideoAsync_WhenPythonServiceReturnsErrorResponse_ReturnsFailureAndMarksAnalysisFailed()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("boom")
        }));

        var service = CreateService(handler);

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await service.AnalyzeVideoAsync(stream, "clip.mp4", 1);

        Assert.False(result.Success);
        Assert.Equal("Python service error: boom", result.Error);

        var analysis = Assert.Single(_analyses);
        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("Python service error: boom", analysis.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeVideoAsync_WhenPythonServiceReturnsInvalidPayload_ReturnsFailureAndMarksAnalysisFailed()
    {
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
        }));

        var service = CreateService(handler);

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await service.AnalyzeVideoAsync(stream, "clip.mp4", 1);

        Assert.False(result.Success);
        Assert.Equal("Invalid response from Python service", result.Error);

        var analysis = Assert.Single(_analyses);
        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("Invalid response from Python service", analysis.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzeVideoAsync_WhenHttpClientThrows_ReturnsFailureAndMarksAnalysisFailed()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("service unavailable"));
        var service = CreateService(handler);

        await using var stream = new MemoryStream([1, 2, 3]);
        var result = await service.AnalyzeVideoAsync(stream, "clip.mp4", 1);

        Assert.False(result.Success);
        Assert.Equal("service unavailable", result.Error);

        var analysis = Assert.Single(_analyses);
        Assert.Equal(AnalysisStatus.Failed, analysis.Status);
        Assert.Equal("service unavailable", analysis.ErrorMessage);
    }

    [Fact]
    public async Task IsServiceHealthyAsync_WhenHealthEndpointReturnsSuccess_ReturnsTrue()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("http://fish.test/health", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        var service = CreateService(handler, "http://fish.test");

        var result = await service.IsServiceHealthyAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsServiceHealthyAsync_WhenRequestThrows_ReturnsFalse()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var result = await service.IsServiceHealthyAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task GetSupportedFishTypesAsync_WhenServiceReturnsFishTypes_ReturnsList()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal("http://fish.test/api/supported-fish", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"fishTypes\":[\"Carp\",\"Pike\"],\"total\":2}",
                    Encoding.UTF8,
                    "application/json")
            });
        });

        var service = CreateService(handler, "http://fish.test");

        var result = await service.GetSupportedFishTypesAsync();

        Assert.Equal(["Carp", "Pike"], result);
    }

    [Fact]
    public async Task GetSupportedFishTypesAsync_WhenRequestFails_ReturnsEmptyList()
    {
        var handler = new StubHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var result = await service.GetSupportedFishTypesAsync();

        Assert.Empty(result);
    }

    private FishRecognitionService CreateService(HttpMessageHandler handler, string serviceUrl = "http://fish.test")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FishRecognitionService:Url"] = serviceUrl
            })
            .Build();

        var httpClient = new HttpClient(handler);
        var imageRepository = Substitute.For<IRepository<ImageAnalysis>>();
        return new FishRecognitionService(httpClient, _videoRepository, imageRepository, configuration);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return _handler(request, cancellationToken);
        }
    }
}