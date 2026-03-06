using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using System.Security.Claims;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BookingsController : ControllerBase
{
    private readonly IRepository<FishingSession> _sessionRepository;
    private readonly IRepository<FishingSpot> _spotRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IEmailService _emailService;

    public BookingsController(
        IRepository<FishingSession> sessionRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<User> userRepository,
        IEmailService emailService)
    {
        _sessionRepository = sessionRepository;
        _spotRepository = spotRepository;
        _userRepository = userRepository;
        _emailService = emailService;
    }

    // GET api/bookings - returns bookings for the logged-in user
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetMyBookings()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var sessions = await _sessionRepository.FindAsync(s => s.UserId == userId.Value);
        var spots = await _spotRepository.GetAllAsync();
        var spotMap = spots.ToDictionary(s => s.Id, s => s.Name);

        return Ok(sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s, spotMap.GetValueOrDefault(s.FishingSpotId, "Unknown"))));
    }

    // GET api/bookings/all - Admin: returns all bookings
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAllBookings()
    {
        var sessions = await _sessionRepository.GetAllAsync();
        var spots = await _spotRepository.GetAllAsync();
        var spotMap = spots.ToDictionary(s => s.Id, s => s.Name);

        return Ok(sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s, spotMap.GetValueOrDefault(s.FishingSpotId, "Unknown"))));
    }

    // POST api/bookings - create a booking
    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking(CreateBookingDto dto)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var spot = await _spotRepository.GetByIdAsync(dto.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found.");

        var allowedDurations = new[] { 12, 24, 48, 72 };
        if (!allowedDurations.Contains(dto.DurationHours))
            return BadRequest("Duration must be 12, 24, 48 or 72 hours.");

        if (dto.StartDate < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Start date cannot be in the past.");

        // Overlap check: prevent duplicate bookings at the same spot in the same time interval
        var newStart = dto.StartDate.ToUniversalTime();
        var newEnd = newStart.AddHours(dto.DurationHours);
        var existingSessions = await _sessionRepository.FindAsync(s =>
            s.FishingSpotId == dto.FishingSpotId &&
            s.Status != SessionStatus.Cancelled);
        var hasOverlap = existingSessions.Any(s =>
            newStart < s.StartDate.AddHours(s.DurationHours) &&
            newEnd > s.StartDate);
        if (hasOverlap)
            return Conflict("This fishing spot is already booked during the selected time interval.");

        var totalPrice = spot.PricePerHour * dto.DurationHours;

        var session = new FishingSession
        {
            UserId = userId.Value,
            FishingSpotId = dto.FishingSpotId,
            StartDate = dto.StartDate.ToUniversalTime(),
            DurationHours = dto.DurationHours,
            TotalPrice = totalPrice,
            Status = SessionStatus.Confirmed
        };

        await _sessionRepository.AddAsync(session);

        try
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            string? firstName = null;

            if (string.IsNullOrWhiteSpace(email))
            {
                var user = await _userRepository.GetByIdAsync(userId.Value);
                email = user?.Email;
                firstName = user?.FirstName;
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                await _emailService.SendBookingConfirmationEmailAsync(
                    email,
                    firstName,
                    spot.Name,
                    session.StartDate,
                    session.DurationHours,
                    session.TotalPrice,
                    session.Id);
            }
        }
        catch
        {
            // Do not block booking creation if SMTP delivery fails.
        }

        return CreatedAtAction(nameof(GetBooking), new { id = session.Id }, MapToDto(session, spot.Name));
    }

    // GET api/bookings/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (session.UserId != userId.Value && !isAdmin)
            return Forbid();

        var spot = await _spotRepository.GetByIdAsync(session.FishingSpotId);
        return Ok(MapToDto(session, spot?.Name ?? "Unknown"));
    }

    // DELETE api/bookings/{id} - cancel a booking
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (session.UserId != userId.Value && !isAdmin)
            return Forbid();

        if (session.Status == SessionStatus.Cancelled)
            return BadRequest("Booking is already cancelled.");

        session.Status = SessionStatus.Cancelled;
        await _sessionRepository.UpdateAsync(session);
        return NoContent();
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    private static BookingDto MapToDto(FishingSession session, string spotName) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        FishingSpotId = session.FishingSpotId,
        FishingSpotName = spotName,
        StartDate = session.StartDate,
        DurationHours = session.DurationHours,
        TotalPrice = session.TotalPrice,
        Status = session.Status.ToString(),
        CreatedAt = session.CreatedAt
    };
}
