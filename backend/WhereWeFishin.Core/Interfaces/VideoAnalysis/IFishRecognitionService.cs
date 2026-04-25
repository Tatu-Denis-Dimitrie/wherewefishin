using WhereWeFishin.Core.DTOs;

namespace WhereWeFishin.Core.Interfaces;

public interface IFishRecognitionService
{
    Task<AnalysisResultDto> AnalyzeVideoAsync(Stream videoStream, string fileName, int userId);
    Task<ImageAnalysisResultDto> AnalyzeImageAsync(Stream imageStream, string fileName, int userId);
    Task<bool> IsServiceHealthyAsync();
    Task<List<string>> GetSupportedFishTypesAsync();
}
