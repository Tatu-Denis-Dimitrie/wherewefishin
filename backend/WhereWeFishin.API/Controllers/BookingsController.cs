using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using WhereWeFishin.API.Extensions;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;
using System.Security.Claims;
using System.Globalization;
using Stripe;

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
    private readonly bool _stripeEnabled;

    public BookingsController(
        IRepository<FishingSession> sessionRepository,
        IRepository<FishingSpot> spotRepository,
        IRepository<Pontoon> pontoonRepository,
        IRepository<User> userRepository,
        IEmailService emailService,
        ILogger<BookingsController> logger,
        IConfiguration configuration)
    {
        _sessionRepository = sessionRepository;
        _spotRepository = spotRepository;
        _pontoonRepository = pontoonRepository;
        _userRepository = userRepository;
        _emailService = emailService;
        _logger = logger;
        _stripeEnabled = !string.IsNullOrWhiteSpace(configuration["Stripe:SecretKey"]);
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

    // POST api/bookings/payment-intent - creates a Stripe PaymentIntent for a booking candidate
    [HttpPost("payment-intent")]
    public async Task<ActionResult<PaymentIntentDto>> CreatePaymentIntent([FromBody] CreatePaymentIntentDto dto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (!_stripeEnabled)
            return StatusCode(503, "Stripe payment is not configured on server.");

        var validation = await ValidateBookingRequestAsync(dto.FishingSpotId, dto.PontoonId, dto.StartDate, dto.DurationHours);
        if (validation.ErrorResult != null)
            return validation.ErrorResult;

        if (validation.TotalPrice <= 0m)
            return BadRequest("Booking total must be greater than 0. Please set a price per hour greater than 0 for this fishing spot.");

        var options = new PaymentIntentCreateOptions
        {
            Amount = ToStripeAmount(validation.TotalPrice),
            Currency = "ron",
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true
            },
            Description = validation.Pontoon != null
                ? $"Fishing session: {validation.Spot!.Name} - {validation.Pontoon.Name}"
                : $"Fishing session: {validation.Spot!.Name}",
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId.Value.ToString(),
                ["fishingSpotId"] = dto.FishingSpotId.ToString(),
                ["pontoonId"] = dto.PontoonId?.ToString() ?? string.Empty,
                ["durationHours"] = dto.DurationHours.ToString(),
                ["startDateUtc"] = validation.StartUtc.ToString("O")
            }
        };

        try
        {
            var paymentIntent = await new PaymentIntentService().CreateAsync(options);

            if (string.IsNullOrWhiteSpace(paymentIntent.ClientSecret))
                return StatusCode(502, "Payment provider did not return a client secret.");

            return Ok(new PaymentIntentDto
            {
                PaymentIntentId = paymentIntent.Id,
                ClientSecret = paymentIntent.ClientSecret,
                Amount = paymentIntent.Amount,
                Currency = paymentIntent.Currency
            });
        }
        catch (StripeException ex)
        {
            var stripeMessage = ex.StripeError?.Message ?? ex.Message;
            _logger.LogWarning(ex, "Stripe create payment intent failed for user {UserId}: {StripeMessage}", userId.Value, stripeMessage);
            return BadRequest($"Stripe could not initialize payment: {stripeMessage}");
        }
    }

    // POST api/bookings - create a booking
    [HttpPost]
    public async Task<ActionResult<BookingDto>> CreateBooking([FromBody] CreateBookingDto dto)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        var validation = await ValidateBookingRequestAsync(dto.FishingSpotId, dto.PontoonId, dto.StartDate, dto.DurationHours);
        if (validation.ErrorResult != null)
            return validation.ErrorResult;

        if (_stripeEnabled)
        {
            if (string.IsNullOrWhiteSpace(dto.PaymentIntentId))
                return BadRequest("Payment is required before creating a booking.");

            var paymentValidationError = await ValidateStripePaymentAsync(
                dto.PaymentIntentId,
                userId.Value,
                dto.FishingSpotId,
                dto.PontoonId,
                validation.StartUtc,
                dto.DurationHours,
                validation.TotalPrice);

            if (!string.IsNullOrWhiteSpace(paymentValidationError))
                return BadRequest(paymentValidationError);
        }

        var session = new FishingSession
        {
            UserId = userId.Value,
            FishingSpotId = dto.FishingSpotId,
            PontoonId = dto.PontoonId,
            StartDate = validation.StartUtc,
            DurationHours = dto.DurationHours,
            TotalPrice = validation.TotalPrice,
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
                var bookingName = validation.Pontoon != null
                    ? $"{validation.Spot!.Name} - {validation.Pontoon.Name}"
                    : validation.Spot!.Name;
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

        return CreatedAtAction(nameof(GetBooking), new { id = session.Id }, MapToDto(session, validation.Spot!.Name, validation.Pontoon?.Name));
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

    private async Task<(FishingSpot? Spot, Pontoon? Pontoon, DateTime StartUtc, DateTime EndUtc, decimal TotalPrice, ActionResult? ErrorResult)>
        ValidateBookingRequestAsync(int fishingSpotId, int? pontoonId, DateTime startDate, int durationHours)
    {
        var spot = await _spotRepository.GetByIdAsync(fishingSpotId);
        if (spot == null)
            return (null, null, default, default, 0m, NotFound("Fishing spot not found."));

        Pontoon? pontoon = null;
        if (pontoonId.HasValue)
        {
            pontoon = await _pontoonRepository.GetByIdAsync(pontoonId.Value);
            if (pontoon == null)
                return (null, null, default, default, 0m, NotFound("Pontoon not found."));

            if (pontoon.FishingSpotId != fishingSpotId)
                return (null, null, default, default, 0m, BadRequest("Pontoon does not belong to this fishing spot."));
        }

        var allowedDurations = new[] { 12, 24, 48, 72 };
        if (!allowedDurations.Contains(durationHours))
            return (null, null, default, default, 0m, BadRequest("Duration must be 12, 24, 48 or 72 hours."));

        if (startDate < DateTime.UtcNow.AddMinutes(-5))
            return (null, null, default, default, 0m, BadRequest("Start date cannot be in the past."));

        var startUtc = startDate.ToUniversalTime();
        var endUtc = startUtc.AddHours(durationHours);

        IEnumerable<FishingSession> existingSessions;
        if (pontoonId.HasValue)
        {
            existingSessions = await _sessionRepository.FindAsync(s =>
                s.PontoonId == pontoonId.Value &&
                s.Status != SessionStatus.Cancelled);
        }
        else
        {
            existingSessions = await _sessionRepository.FindAsync(s =>
                s.FishingSpotId == fishingSpotId &&
                s.PontoonId == null &&
                s.Status != SessionStatus.Cancelled);
        }

        var hasOverlap = existingSessions.Any(s =>
            startUtc < s.StartDate.AddHours(s.DurationHours) &&
            endUtc > s.StartDate);

        if (hasOverlap)
        {
            return (null, null, default, default, 0m, Conflict(pontoonId.HasValue
                ? "This pontoon is already booked during the selected time interval."
                : "This fishing spot is already booked during the selected time interval."));
        }

        var totalPrice = spot.PricePerHour * durationHours;
        return (spot, pontoon, startUtc, endUtc, totalPrice, null);
    }

    private async Task<string?> ValidateStripePaymentAsync(
        string paymentIntentId,
        int userId,
        int fishingSpotId,
        int? pontoonId,
        DateTime startUtc,
        int durationHours,
        decimal expectedTotalPrice)
    {
        PaymentIntent paymentIntent;
        try
        {
            paymentIntent = await new PaymentIntentService().GetAsync(paymentIntentId);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe payment intent lookup failed for {PaymentIntentId}", paymentIntentId);
            return "Invalid payment reference.";
        }

        if (!string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
            return "Payment is not completed.";

        if (!string.Equals(paymentIntent.Currency, "ron", StringComparison.OrdinalIgnoreCase))
            return "Payment currency is invalid.";

        var expectedAmount = ToStripeAmount(expectedTotalPrice);
        var receivedAmount = paymentIntent.AmountReceived > 0 ? paymentIntent.AmountReceived : paymentIntent.Amount;
        if (receivedAmount != expectedAmount)
            return "Payment amount does not match booking total.";

        if (!MetadataMatches(paymentIntent.Metadata, "userId", userId.ToString()))
            return "Payment does not belong to the current user.";

        if (!MetadataMatches(paymentIntent.Metadata, "fishingSpotId", fishingSpotId.ToString()))
            return "Payment details do not match the selected fishing spot.";

        if (!MetadataMatches(paymentIntent.Metadata, "pontoonId", pontoonId?.ToString() ?? string.Empty))
            return "Payment details do not match the selected pontoon.";

        if (!MetadataMatches(paymentIntent.Metadata, "durationHours", durationHours.ToString()))
            return "Payment details do not match the selected duration.";

        if (!paymentIntent.Metadata.TryGetValue("startDateUtc", out var startDateRaw) || string.IsNullOrWhiteSpace(startDateRaw))
            return "Payment details are incomplete.";

        if (!DateTime.TryParse(startDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var paymentStartUtc))
            return "Payment details are invalid.";

        if (paymentStartUtc.ToUniversalTime() != startUtc)
            return "Payment details do not match the selected start date.";

        return null;
    }

    private static bool MetadataMatches(IDictionary<string, string> metadata, string key, string expected)
        => metadata.TryGetValue(key, out var value) && string.Equals(value ?? string.Empty, expected, StringComparison.Ordinal);

    private static long ToStripeAmount(decimal amount)
        => Convert.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

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
