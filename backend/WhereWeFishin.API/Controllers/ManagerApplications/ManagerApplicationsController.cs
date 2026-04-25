using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Extensions;
using WhereWeFishin.Database.Context;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ManagerApplicationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IOutputCacheStore _cacheStore;

    public ManagerApplicationsController(ApplicationDbContext context, IOutputCacheStore cacheStore)
    {
        _context = context;
        _cacheStore = cacheStore;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ManagerApplicationDto>>> GetMyApplications()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var applications = await _context.ManagerApplications
            .AsNoTracking()
            .Include(application => application.ApplicantUser)
            .Include(application => application.ReviewedByAdmin)
            .Where(application => application.ApplicantUserId == userId.Value)
            .OrderByDescending(application => application.CreatedAt)
            .ToListAsync();

        return Ok(applications.Select(MapToDto));
    }

    [HttpPost]
    public async Task<ActionResult<ManagerApplicationDto>> Create([FromBody] CreateManagerApplicationDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        if (User.IsInRole(Roles.Admin))
            return BadRequest(new { message = "Admin users can create fishing spots directly." });

        var hasPendingApplication = await _context.ManagerApplications
            .AnyAsync(application => application.ApplicantUserId == userId.Value && application.Status == ManagerApplicationStatus.Pending);

        if (hasPendingApplication)
            return Conflict(new { message = "You already have a pending manager application." });

        var application = new ManagerApplication
        {
            ApplicantUserId = userId.Value,
            LakeName = dto.LakeName.Trim(),
            Description = NormalizeOptional(dto.Description),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            LocationLabel = NormalizeOptional(dto.LocationLabel),
            ProposedPricePerHour = dto.ProposedPricePerHour,
            FishSpecies = NormalizeOptional(dto.FishSpecies),
            ContactPhone = dto.ContactPhone.Trim(),
            Motivation = dto.Motivation.Trim(),
            AdministrationBasis = dto.AdministrationBasis.Trim()
        };

        _context.ManagerApplications.Add(application);
        await _context.SaveChangesAsync();

        application = await LoadApplicationAsync(application.Id) ?? application;
        return CreatedAtAction(nameof(GetMyApplications), new { id = application.Id }, MapToDto(application));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ManagerApplicationDto>> Update(int id, [FromBody] UpdateManagerApplicationDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var application = await _context.ManagerApplications.FirstOrDefaultAsync(item => item.Id == id);
        if (application == null) return NotFound();
        if (application.ApplicantUserId != userId.Value) return Forbid();
        if (application.Status != ManagerApplicationStatus.Rejected)
            return BadRequest(new { message = "Only rejected applications can be edited." });

        ApplyChanges(application, dto);
        await _context.SaveChangesAsync();

        application = await LoadApplicationAsync(application.Id) ?? application;
        return Ok(MapToDto(application));
    }

    [HttpPost("{id}/resubmit")]
    public async Task<ActionResult<ManagerApplicationDto>> Resubmit(int id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var application = await _context.ManagerApplications.FirstOrDefaultAsync(item => item.Id == id);
        if (application == null) return NotFound();
        if (application.ApplicantUserId != userId.Value) return Forbid();
        if (application.Status != ManagerApplicationStatus.Rejected)
            return BadRequest(new { message = "Only rejected applications can be resubmitted." });

        var hasAnotherPendingApplication = await _context.ManagerApplications
            .AnyAsync(item => item.ApplicantUserId == userId.Value
                && item.Id != id
                && item.Status == ManagerApplicationStatus.Pending);

        if (hasAnotherPendingApplication)
            return Conflict(new { message = "You already have another pending manager application." });

        application.Status = ManagerApplicationStatus.Pending;
        application.RejectionReason = null;
        application.ReviewedAt = null;
        application.ReviewedByAdminId = null;

        await _context.SaveChangesAsync();

        application = await LoadApplicationAsync(application.Id) ?? application;
        return Ok(MapToDto(application));
    }

    [HttpGet("pending")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<IEnumerable<ManagerApplicationDto>>> GetPendingApplications()
    {
        var applications = await GetPendingApplicationsQuery().ToListAsync();
        return Ok(applications.Select(MapToDto));
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManagerApplicationDto>> Approve(int id)
    {
        var adminId = User.GetUserId();
        if (adminId == null) return Unauthorized();

        var application = await _context.ManagerApplications
            .Include(item => item.ApplicantUser)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (application == null) return NotFound();
        if (application.Status != ManagerApplicationStatus.Pending)
            return BadRequest(new { message = "Only pending applications can be approved." });

        var fishingSpot = new FishingSpot
        {
            Name = application.LakeName,
            Description = application.Description,
            Latitude = application.Latitude,
            Longitude = application.Longitude,
            PricePerHour = application.ProposedPricePerHour,
            UserId = application.ApplicantUserId,
            ManagerId = application.ApplicantUserId,
            FishSpecies = application.FishSpecies
        };

        _context.FishingSpots.Add(fishingSpot);

        if (application.ApplicantUser.Role != UserRole.Manager)
        {
            application.ApplicantUser.Role = UserRole.Manager;
        }

        application.Status = ManagerApplicationStatus.Approved;
        application.RejectionReason = null;
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByAdminId = adminId.Value;
        application.ApprovedFishingSpot = fishingSpot;

        await _context.SaveChangesAsync();
        await _cacheStore.EvictByTagAsync("fishingspots", default);

        application = await LoadApplicationAsync(application.Id) ?? application;
        return Ok(MapToDto(application));
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ManagerApplicationDto>> Reject(int id, [FromBody] RejectManagerApplicationDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var adminId = User.GetUserId();
        if (adminId == null) return Unauthorized();

        var application = await _context.ManagerApplications.FirstOrDefaultAsync(item => item.Id == id);
        if (application == null) return NotFound();
        if (application.Status != ManagerApplicationStatus.Pending)
            return BadRequest(new { message = "Only pending applications can be rejected." });

        application.Status = ManagerApplicationStatus.Rejected;
        application.RejectionReason = dto.Reason.Trim();
        application.ReviewedAt = DateTime.UtcNow;
        application.ReviewedByAdminId = adminId.Value;
        application.ApprovedFishingSpotId = null;

        await _context.SaveChangesAsync();

        application = await LoadApplicationAsync(application.Id) ?? application;
        return Ok(MapToDto(application));
    }

    private IQueryable<ManagerApplication> GetPendingApplicationsQuery()
    {
        return _context.ManagerApplications
            .AsNoTracking()
            .Include(application => application.ApplicantUser)
            .Include(application => application.ReviewedByAdmin)
            .Where(application => application.Status == ManagerApplicationStatus.Pending)
            .OrderBy(application => application.CreatedAt);
    }

    private Task<ManagerApplication?> LoadApplicationAsync(int id)
    {
        return _context.ManagerApplications
            .AsNoTracking()
            .Include(application => application.ApplicantUser)
            .Include(application => application.ReviewedByAdmin)
            .FirstOrDefaultAsync(application => application.Id == id);
    }

    private static void ApplyChanges(ManagerApplication application, UpdateManagerApplicationDto dto)
    {
        application.LakeName = dto.LakeName.Trim();
        application.Description = NormalizeOptional(dto.Description);
        application.Latitude = dto.Latitude;
        application.Longitude = dto.Longitude;
        application.LocationLabel = NormalizeOptional(dto.LocationLabel);
        application.ProposedPricePerHour = dto.ProposedPricePerHour;
        application.FishSpecies = NormalizeOptional(dto.FishSpecies);
        application.ContactPhone = dto.ContactPhone.Trim();
        application.Motivation = dto.Motivation.Trim();
        application.AdministrationBasis = dto.AdministrationBasis.Trim();
    }

    private static ManagerApplicationDto MapToDto(ManagerApplication application)
    {
        var applicantUsername = application.ApplicantUser?.Username ?? string.Empty;
        return new ManagerApplicationDto
        {
            Id = application.Id,
            ApplicantUserId = application.ApplicantUserId,
            ApplicantUsername = applicantUsername,
            ApplicantDisplayName = application.ApplicantUser != null
                ? UserExtensions.GetDisplayName(application.ApplicantUser.FirstName, application.ApplicantUser.LastName, applicantUsername)
                : applicantUsername,
            LakeName = application.LakeName,
            Description = application.Description,
            Latitude = application.Latitude,
            Longitude = application.Longitude,
            LocationLabel = application.LocationLabel,
            ProposedPricePerHour = application.ProposedPricePerHour,
            FishSpecies = application.FishSpecies,
            ContactPhone = application.ContactPhone,
            Motivation = application.Motivation,
            AdministrationBasis = application.AdministrationBasis,
            Status = application.Status.ToString(),
            RejectionReason = application.RejectionReason,
            ReviewedAt = application.ReviewedAt,
            ReviewedByAdminId = application.ReviewedByAdminId,
            ReviewedByAdminName = application.ReviewedByAdmin != null
                ? UserExtensions.GetDisplayName(application.ReviewedByAdmin.FirstName, application.ReviewedByAdmin.LastName, application.ReviewedByAdmin.Username)
                : null,
            ApprovedFishingSpotId = application.ApprovedFishingSpotId,
            CreatedAt = application.CreatedAt,
            UpdatedAt = application.UpdatedAt
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim();
    }
}