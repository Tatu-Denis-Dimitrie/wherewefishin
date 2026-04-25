namespace WhereWeFishin.Core.DTOs;

public class AdminHomeOverviewDto
{
    public int ActiveUsers { get; set; }
    public int DeactivatedUsers { get; set; }
    public int TotalSpots { get; set; }
    public int SpotsWithoutManager { get; set; }
    public int PendingManagerApplications { get; set; }
    public int RejectedManagerApplications { get; set; }
    public int FailedVideoAnalyses { get; set; }
    public int CancelledBookings { get; set; }
    public List<ManagerApplicationDto> PendingApplications { get; set; } = [];
}