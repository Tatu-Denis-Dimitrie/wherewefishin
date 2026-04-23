using System.ComponentModel.DataAnnotations;
using WhereWeFishin.Core.DTOs;

namespace WhereWeFishin.Tests.DTOs;

public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void LoginRequest_WithValidData_PassesValidation()
    {
        var results = Validate(new LoginRequest
        {
            UsernameOrEmail = "angler@test.com",
            Password = "password123"
        });

        Assert.Empty(results);
    }

    [Fact]
    public void LoginRequest_WithoutRequiredFields_FailsValidation()
    {
        var results = Validate(new LoginRequest());

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void RegisterRequest_WithValidData_PassesValidation()
    {
        var results = Validate(new RegisterRequest
        {
            Username = "angler123",
            Email = "angler@test.com",
            Password = "password123",
            ConfirmPassword = "password123"
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("ab", "angler@test.com", "password123", "password123")]
    [InlineData("angler123", "invalid-email", "password123", "password123")]
    [InlineData("angler123", "angler@test.com", "12345", "12345")]
    [InlineData("angler123", "angler@test.com", "password123", "different")]
    public void RegisterRequest_WithInvalidData_FailsValidation(string username, string email, string password, string confirmPassword)
    {
        var results = Validate(new RegisterRequest
        {
            Username = username,
            Email = email,
            Password = password,
            ConfirmPassword = confirmPassword
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ForgotPasswordRequest_WithInvalidEmail_FailsValidation()
    {
        var results = Validate(new ForgotPasswordRequest { Email = "invalid" });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ForgotPasswordRequest_WithValidEmail_PassesValidation()
    {
        var results = Validate(new ForgotPasswordRequest { Email = "angler@test.com" });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("invalid", "123456", "newpassword123")]
    [InlineData("angler@test.com", "12345", "newpassword123")]
    [InlineData("angler@test.com", "123456", "12345")]
    public void ResetPasswordRequest_WithInvalidData_FailsValidation(string email, string code, string newPassword)
    {
        var results = Validate(new ResetPasswordRequest
        {
            Email = email,
            Code = code,
            NewPassword = newPassword
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ResetPasswordRequest_WithValidData_PassesValidation()
    {
        var results = Validate(new ResetPasswordRequest
        {
            Email = "angler@test.com",
            Code = "123456",
            NewPassword = "newpassword123"
        });

        Assert.Empty(results);
    }

    [Fact]
    public void ChangePasswordRequest_WithShortNewPassword_FailsValidation()
    {
        var results = Validate(new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "12345"
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void ChangePasswordRequest_WithValidData_PassesValidation()
    {
        var results = Validate(new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123"
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(1, 0)]
    [InlineData(1, 8761)]
    public void CreateBookingDto_WithOutOfRangeValues_FailsValidation(int fishingSpotId, int durationHours)
    {
        var results = Validate(new CreateBookingDto
        {
            FishingSpotId = fishingSpotId,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = durationHours
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateBookingDto_WithValidData_PassesValidation()
    {
        var results = Validate(new CreateBookingDto
        {
            FishingSpotId = 1,
            StartDate = DateTime.UtcNow.AddDays(1),
            DurationHours = 24
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 0)]
    [InlineData(1, 6)]
    public void CreateReviewDto_WithInvalidValues_FailsValidation(int fishingSpotId, int rating)
    {
        var results = Validate(new CreateReviewDto
        {
            FishingSpotId = fishingSpotId,
            Rating = rating
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateReviewDto_WithValidData_PassesValidation()
    {
        var results = Validate(new CreateReviewDto
        {
            FishingSpotId = 1,
            Rating = 5,
            Comment = "Excellent place"
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void UpdateReviewDto_WithInvalidRating_FailsValidation(int rating)
    {
        var results = Validate(new UpdateReviewDto { Rating = rating });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void UpdateReviewDto_WithValidRating_PassesValidation()
    {
        var results = Validate(new UpdateReviewDto { Rating = 4, Comment = "Updated comment" });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("", 45.0, 25.0, 10.0)]
    [InlineData("Spot", 91.0, 25.0, 10.0)]
    [InlineData("Spot", 45.0, 181.0, 10.0)]
    [InlineData("Spot", 45.0, 25.0, -1.0)]
    public void CreateFishingSpotDto_WithInvalidValues_FailsValidation(string name, double latitude, double longitude, decimal pricePerHour)
    {
        var results = Validate(new CreateFishingSpotDto
        {
            Name = name,
            Latitude = latitude,
            Longitude = longitude,
            PricePerHour = pricePerHour
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateFishingSpotDto_WithValidData_PassesValidation()
    {
        var results = Validate(new CreateFishingSpotDto
        {
            Name = "Danube Bank",
            Description = "Quiet spot",
            Latitude = 45.0,
            Longitude = 25.0,
            PricePerHour = 30m
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData(0, "Pontoon A")]
    [InlineData(1, "")]
    public void CreatePontoonDto_WithInvalidValues_FailsValidation(int fishingSpotId, string name)
    {
        var results = Validate(new CreatePontoonDto
        {
            FishingSpotId = fishingSpotId,
            Name = name
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreatePontoonDto_WithValidData_PassesValidation()
    {
        var results = Validate(new CreatePontoonDto
        {
            FishingSpotId = 1,
            Name = "Pontoon A",
            Color = "#3388ff"
        });

        Assert.Empty(results);
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Carp", 0)]
    public void CreateFishStockingDto_WithInvalidValues_FailsValidation(string species, int quantity)
    {
        var results = Validate(new CreateFishStockingDto
        {
            StockingDate = DateTime.UtcNow,
            Species = species,
            Quantity = quantity
        });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void CreateFishStockingDto_WithValidData_PassesValidation()
    {
        var results = Validate(new CreateFishStockingDto
        {
            StockingDate = DateTime.UtcNow,
            Species = "Carp",
            Quantity = 100,
            Notes = "Spring stocking"
        });

        Assert.Empty(results);
    }

    [Fact]
    public void UpdateFishStockingDto_WithInvalidQuantity_FailsValidation()
    {
        var results = Validate(new UpdateFishStockingDto { Quantity = 0 });

        Assert.NotEmpty(results);
    }

    [Fact]
    public void UpdateFishStockingDto_WithValidData_PassesValidation()
    {
        var results = Validate(new UpdateFishStockingDto
        {
            Species = "Pike",
            Quantity = 200,
            Notes = "Updated"
        });

        Assert.Empty(results);
    }
}