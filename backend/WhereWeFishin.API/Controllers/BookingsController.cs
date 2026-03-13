using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.API.Extensions;
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
    private readonly IRepository<Pontoon> _pontoonRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<BookingsController> _logger;

    public BookingsController(
        IRepository<FishingSession> sessionRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<Pontoon> pontoonRepository,
        IRepository<User> userRepository,
        IEmailService emailService,
        ILogger<BookingsController> logger)
    {
        _sessionRepository = sessionRepository;
        _spotRepository = spotRepository;
        _pontoonRepository = pontoonRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
    }

    // GET api/bookings - returns bookings for the logged-in user
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetMyBookings()
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var sessions = await _sessionRepository.FindAsync(s => s.UserId == userId.Value);
        var spots = await _spotRepository.GetAllAsync();
        var pontoons = await _pontoonRepository.GetAllAsync();
        var spotMap = spots.ToDictionary(s => s.Id, s => s.Name);
        var pontoonMap = pontoons.ToDictionary(p => p.Id, p => p.Name);

        return Ok(sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s, spotMap.GetValueOrDefault(s.FishingSpotId, "Unknown"), 
                                     s.PontoonId.HasValue ? pontoonMap.GetValueOrDefault(s.PontoonId.Value) : null)));
    }

    // GET api/bookings/all - Admin: returns all bookings
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<BookingDto>>> GetAllBookings()
    {
        var sessions = await _sessionRepository.GetAllAsync();
        var spots = await _spotRepository.GetAllAsync();
        var pontoons = await _pontoonRepository.GetAllAsync();
        var spotMap = spots.ToDictionary(s => s.Id, s => s.Name);
        var pontoonMap = pontoons.ToDictionary(p => p.Id, p => p.Name);

        return Ok(sessions
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => MapToDto(s, spotMap.GetValueOrDefault(s.FishingSpotId, "Unknown"),
                                     s.PontoonId.HasValue ? pontoonMap.GetValueOrDefault(s.PontoonId.Value) : null)));
    }

    // POST api/bookings - create a booking
    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingDto dto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var spot = await _spotRepository.GetByIdAsync(dto.FishingSpotId);
        if (spot == null) return NotFound("Fishing spot not found.");

        Pontoon? pontoon = null;
        if (dto.PontoonId.HasValue)
        {
            pontoon = await _pontoonRepository.GetByIdAsync(dto.PontoonId.Value);
            if (pontoon == null) return NotFound("Pontoon not found.");
            if (pontoon.FishingSpotId != dto.FishingSpotId) 
                return BadRequest("Pontoon does not belong to this fishing spot.");
        }

        var allowedDurations = new[] { 12, 24, 48, 72 };
        if (!allowedDurations.Contains(dto.DurationHours))
            return BadRequest("Duration must be 12, 24, 48 or 72 hours.");

        if (dto.StartDate < DateTime.UtcNow.AddMinutes(-5))
            return BadRequest("Start date cannot be in the past.");

        // Overlap check: if pontoon is specified, check pontoon overlap; otherwise check spot overlap
        var newStart = dto.StartDate.ToUniversalTime();
        var newEnd = newStart.AddHours(dto.DurationHours);
        
        IEnumerable<FishingSession> existingSessions;
        if (dto.PontoonId.HasValue)
        {
            existingSessions = await _sessionRepository.FindAsync(s =>
                s.PontoonId == dto.PontoonId.Value &&
                s.Status != SessionStatus.Cancelled);
        }
        else
        {
            existingSessions = await _sessionRepository.FindAsync(s =>
                s.FishingSpotId == dto.FishingSpotId &&
                s.PontoonId == null &&
                s.Status != SessionStatus.Cancelled);
        }
        
        var hasOverlap = existingSessions.Any(s =>
            newStart < s.StartDate.AddHours(s.DurationHours) &&
            newEnd > s.StartDate);
        if (hasOverlap)
            return Conflict(dto.PontoonId.HasValue 
                ? "This pontoon is already booked during the selected time interval."
                : "This fishing spot is already booked during the selected time interval.");

        var totalPrice = spot.PricePerHour * dto.DurationHours;

        var session = new FishingSession
        {
            UserId = userId.Value,
            FishingSpotId = dto.FishingSpotId,
            PontoonId = dto.PontoonId,
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
                var bookingName = pontoon != null ? $"{spot.Name} - {pontoon.Name}" : spot.Name;
                await _emailService.SendBookingConfirmationEmailAsync(
                    email,
                    firstName,
                    bookingName,
                    session.StartDate,
                    session.DurationHours,
                    session.TotalPrice,
                    session.Id);
            }
        }
        catch (Exception ex)
        {
            // Do not block booking creation if SMTP delivery fails.
            _logger.LogWarning(ex, "Booking confirmation email failed for booking {BookingId}", session.Id);
        }

        return CreatedAtAction(nameof(GetBooking), new { id = session.Id }, MapToDto(session, spot.Name, pontoon?.Name));
    }

    // GET api/bookings/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<BookingDto>> GetBooking(int id)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var session = await _sessionRepository.GetByIdAsync(id);
        if (session == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (session.UserId != userId.Value && !isAdmin)
            return Forbid();

        var spot = await _spotRepository.GetByIdAsync(session.FishingSpotId);
        Pontoon? pontoon = null;
        if (session.PontoonId.HasValue)
        {
            pontoon = await _pontoonRepository.GetByIdAsync(session.PontoonId.Value);
        }
        return Ok(MapToDto(session, spot?.Name ?? "Unknown", pontoon?.Name));
    }

    // DELETE api/bookings/{id} - cancel a booking
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var userId = User.GetUserId();
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

    private static BookingDto MapToDto(FishingSession session, string spotName, string? pontoonName = null) => new()
    {
        Id = session.Id,
        UserId = session.UserId,
        FishingSpotId = session.FishingSpotId,
        FishingSpotName = spotName,
        PontoonId = session.PontoonId,
        PontoonName = pontoonName,
        StartDate = session.StartDate,
        DurationHours = session.DurationHours,
        TotalPrice = session.TotalPrice,
        Status = session.Status.ToString(),
        CreatedAt = session.CreatedAt
    };
}
