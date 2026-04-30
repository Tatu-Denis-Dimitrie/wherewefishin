using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeesController : ControllerBase
{
    private readonly IRepository<SpotEmployee> _spotEmployeeRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<Pontoon> _pontoonRepository;

    public EmployeesController(
        IRepository<SpotEmployee> spotEmployeeRepository,
        IRepository<User> userRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<FishingSession> sessionRepository,
        IRepository<Pontoon> pontoonRepository)
    {
        _spotEmployeeRepository = spotEmployeeRepository;
        _userRepository = userRepository;
        _spotRepository = spotRepository;
        _sessionRepository = sessionRepository;
        _pontoonRepository = pontoonRepository;
    }

    [HttpGet("spot/{spotId}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<IEnumerable<SpotEmployeeDto>>> GetSpotEmployees(int spotId)
    {
        var spot = await _spotRepository.GetByIdAsync(spotId);
        if (spot == null) return NotFound("Fishing spot not found.");

        if (!User.IsInRole(Roles.Admin))
        {
            if (!User.CanManageSpot(spot))
                return Forbid();
        }

        var employees = await _spotEmployeeRepository.FindAsync(e => e.FishingSpotId == spotId);
        var userIds = employees.Select(e => e.UserId).Distinct().ToHashSet();
        var users = await _userRepository.FindAsync(u => userIds.Contains(u.Id));
        var userMap = users.ToDictionary(u => u.Id);

        var result = employees.Select(e =>
        {
            userMap.TryGetValue(e.UserId, out var user);
            return new SpotEmployeeDto
            {
                Id = e.Id,
                UserId = e.UserId,
                Username = user?.Username ?? "Unknown",
                FirstName = user?.FirstName,
                LastName = user?.LastName,
                FishingSpotId = e.FishingSpotId,
                FishingSpotName = spot.Name,
                CreatedAt = e.CreatedAt
            };
        });

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<SpotEmployeeDto>> AssignEmployee([FromBody] AssignEmployeeDto dto)
    {
        var spot = await _spotRepository.GetByIdAsync(dto.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found.");

        if (!User.IsInRole(Roles.Admin))
        {
            if (!User.CanManageSpot(spot))
                return Forbid();
        }

        var employee = await _userRepository.GetByIdAsync(dto.UserId);
        if (employee == null) return NotFound("User not found.");
        if (employee.Role != UserRole.Employee)
            return BadRequest("User must have the Employee role to be assigned.");

        var existing = await _spotEmployeeRepository.FindAsync(
            e => e.UserId == dto.UserId && e.FishingSpotId == dto.FishingSpotId);
        if (existing.Any())
            return Conflict("Employee is already assigned to this fishing spot.");

        var spotEmployee = new SpotEmployee
        {
            UserId = dto.UserId,
            FishingSpotId = dto.FishingSpotId
        };

        await _spotEmployeeRepository.AddAsync(spotEmployee);

        return CreatedAtAction(nameof(GetSpotEmployees), new { spotId = dto.FishingSpotId }, new SpotEmployeeDto
        {
            Id = spotEmployee.Id,
            UserId = employee.Id,
            Username = employee.Username,
            FirstName = employee.FirstName,
            LastName = employee.LastName,
            FishingSpotId = spot.Id,
            FishingSpotName = spot.Name,
            CreatedAt = spotEmployee.CreatedAt
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> RemoveEmployee(int id)
    {
        var assignment = await _spotEmployeeRepository.GetByIdAsync(id);
        if (assignment == null) return NotFound();

        if (!User.IsInRole(Roles.Admin))
        {
            var spot = await _spotRepository.GetByIdAsync(assignment.FishingSpotId);
            if (spot == null || !User.CanManageSpot(spot))
                return Forbid();
        }

        await _spotEmployeeRepository.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("available")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAvailableEmployees()
    {
        var employees = await _userRepository.FindAsync(u => u.Role == UserRole.Employee);
        return Ok(employees.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Role = u.Role.ToString(),
            CreatedAt = u.CreatedAt,
            IsActive = !u.IsDeleted
        }));
    }

    [HttpGet("my-spots")]
    [Authorize(Roles = Roles.Employee)]
    public async Task<ActionResult<IEnumerable<SpotEmployeeDto>>> GetMyAssignedSpots()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var assignments = await _spotEmployeeRepository.FindAsync(e => e.UserId == userId.Value);
        var spotIds = assignments.Select(e => e.FishingSpotId).Distinct().ToHashSet();
        var spots = await _spotRepository.FindAsync(s => spotIds.Contains(s.Id));
        var spotMap = spots.ToDictionary(s => s.Id);

        return Ok(assignments.Select(e =>
        {
            spotMap.TryGetValue(e.FishingSpotId, out var spot);
            return new SpotEmployeeDto
            {
                Id = e.Id,
                UserId = e.UserId,
                FishingSpotId = e.FishingSpotId,
                FishingSpotName = spot?.Name ?? "Unknown",
                CreatedAt = e.CreatedAt
            };
        }));
    }

    [HttpGet("overview")]
    [Authorize(Roles = Roles.Employee)]
    public async Task<ActionResult<EmployeeOverviewDto>> GetMyOverview()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var assignments = (await _spotEmployeeRepository.FindAsync(e => e.UserId == userId.Value)).ToList();
        var assignedSpotIds = assignments
            .Select(assignment => assignment.FishingSpotId)
            .Distinct()
            .ToHashSet();

        var verifiedSessions = (await _sessionRepository.FindAsync(session => session.VerifiedByUserId == userId.Value))
            .Where(session => session.VerifiedAt.HasValue)
            .OrderByDescending(session => session.VerifiedAt)
            .ToList();

        var now = DateTime.UtcNow;
        var startOfTodayUtc = now.Date;
        var activeAssignedBookingsCount = assignedSpotIds.Count == 0
            ? 0
            : (await _sessionRepository.FindAsync(session =>
                assignedSpotIds.Contains(session.FishingSpotId) &&
                session.Status == SessionStatus.Confirmed &&
                session.StartDate <= now &&
                session.StartDate.AddHours(session.DurationHours) >= now)).Count();

        var recentVerifiedSessions = verifiedSessions
            .Take(5)
            .ToList();

        var recentSpotIds = recentVerifiedSessions
            .Select(session => session.FishingSpotId)
            .Distinct()
            .ToHashSet();
        var recentUserIds = recentVerifiedSessions
            .Select(session => session.UserId)
            .Distinct()
            .ToHashSet();

        var spots = recentSpotIds.Count == 0
            ? []
            : await _spotRepository.FindAsync(spot => recentSpotIds.Contains(spot.Id));
        var users = recentUserIds.Count == 0
            ? []
            : await _userRepository.FindAsync(user => recentUserIds.Contains(user.Id));

        var spotMap = spots.ToDictionary(spot => spot.Id, spot => spot.Name);
        var userMap = users.ToDictionary(user => user.Id, user => user.Username);

        return Ok(new EmployeeOverviewDto
        {
            AssignedSpotsCount = assignedSpotIds.Count,
            VerifiedQrScansCount = verifiedSessions.Count,
            VerifiedQrScansTodayCount = verifiedSessions.Count(session => session.VerifiedAt >= startOfTodayUtc),
            VerifiedGuestsCount = verifiedSessions.Select(session => session.UserId).Distinct().Count(),
            ActiveAssignedBookingsCount = activeAssignedBookingsCount,
            RecentVerifications = recentVerifiedSessions.Select(session => new EmployeeRecentVerificationDto
            {
                BookingId = session.Id,
                FishingSpotId = session.FishingSpotId,
                FishingSpotName = spotMap.GetValueOrDefault(session.FishingSpotId, "Unknown"),
                Username = userMap.GetValueOrDefault(session.UserId, "Unknown"),
                VerifiedAt = session.VerifiedAt!.Value,
                StartDate = session.StartDate,
                DurationHours = session.DurationHours
            }).ToList()
        });
    }

    [HttpPost("verify-qr")]
    [Authorize(Roles = Roles.EmployeeOrManagerOrAdmin)]
    public async Task<ActionResult<QrVerificationResultDto>> VerifyQr([FromBody] VerifyQrDto dto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionRepository.GetByIdAsync(dto.BookingId);
        if (session == null)
            return Ok(new QrVerificationResultDto { Valid = false, Message = "Booking not found." });

        if (string.IsNullOrWhiteSpace(session.VerificationToken) ||
            !string.Equals(session.VerificationToken, dto.VerificationToken, StringComparison.Ordinal))
            return Ok(new QrVerificationResultDto { Valid = false, Message = "Invalid QR code." });

        if (User.IsInRole(Roles.Employee))
        {
            var assignments = await _spotEmployeeRepository.FindAsync(
                e => e.UserId == userId.Value && e.FishingSpotId == session.FishingSpotId);
            if (!assignments.Any())
                return Ok(new QrVerificationResultDto { Valid = false, Message = "You are not assigned to this spot." });
        }

        var spot = await _spotRepository.GetByIdAsync(session.FishingSpotId);
        Pontoon? pontoon = null;
        if (session.PontoonId.HasValue)
            pontoon = await _pontoonRepository.GetByIdAsync(session.PontoonId.Value);

        var bookingUser = await _userRepository.GetByIdAsync(session.UserId);

        var now = DateTime.UtcNow;
        var endDate = session.StartDate.AddHours(session.DurationHours);
        var isActive = session.Status == SessionStatus.Confirmed && now >= session.StartDate && now <= endDate;
        var isExpired = now > endDate;
        var isCancelled = session.Status == SessionStatus.Cancelled;

        string message;
        if (isCancelled)
            message = "Booking was cancelled.";
        else if (isExpired)
            message = "Fishing session has expired.";
        else if (!isActive)
            message = $"Session has not started yet. Starts at: {session.StartDate:dd.MM.yyyy HH:mm} UTC.";
        else
            message = "Valid booking! Fishing session is active.";

        if (isActive && User.IsInRole(Roles.Employee) && session.VerifiedByUserId == null)
        {
            session.VerifiedByUserId = userId.Value;
            session.VerifiedAt = now;
            await _sessionRepository.UpdateAsync(session);
        }

        return Ok(new QrVerificationResultDto
        {
            Valid = isActive,
            Message = message,
            BookingId = session.Id,
            Username = bookingUser?.Username,
            FishingSpotName = spot?.Name,
            PontoonName = pontoon?.Name,
            StartDate = session.StartDate,
            DurationHours = session.DurationHours,
            TotalPrice = session.TotalPrice,
            Status = session.Status.ToString()
        });
    }
}
