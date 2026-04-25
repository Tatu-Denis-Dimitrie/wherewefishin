using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class ManagerApplicationsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly IOutputCacheStore _cacheStore;
    private readonly ManagerApplicationsController _controller;

    public ManagerApplicationsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _cacheStore = Substitute.For<IOutputCacheStore>();
        _controller = new ManagerApplicationsController(_context, _cacheStore);
    }

    [Fact]
    public async Task Create_WhenPendingApplicationExists_ReturnsConflict()
    {
        var user = AddUser(15, UserRole.User, "angler15");
        AddApplication(1, user.Id, ManagerApplicationStatus.Pending);
        ControllerContextFactory.SetAuthenticatedUser(_controller, user.Id, Roles.User, user.Username, user.Email);

        var result = await _controller.Create(new CreateManagerApplicationDto
        {
            LakeName = "New Proposal",
            Latitude = 45.1,
            Longitude = 25.2,
            ContactPhone = "0712345678",
            Motivation = "I can manage it well.",
            AdministrationBasis = "Owner"
        });

        Assert.IsType<ConflictObjectResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenRejectedApplication_UpdatesFields()
    {
        var user = AddUser(16, UserRole.User, "angler16");
        var application = AddApplication(2, user.Id, ManagerApplicationStatus.Rejected);
        ControllerContextFactory.SetAuthenticatedUser(_controller, user.Id, Roles.User, user.Username, user.Email);

        var result = await _controller.Update(application.Id, new UpdateManagerApplicationDto
        {
            LakeName = "Updated Lake",
            Description = "Updated description",
            Latitude = 46.2,
            Longitude = 26.4,
            LocationLabel = "Updated location",
            ProposedPricePerHour = 35,
            FishSpecies = "[\"Carp\"]",
            ContactPhone = "0722222222",
            Motivation = "Updated motivation",
            AdministrationBasis = "Concession"
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ManagerApplicationDto>(okResult.Value);
        Assert.Equal("Updated Lake", dto.LakeName);
        Assert.Equal("Updated location", dto.LocationLabel);
        Assert.Equal(35, dto.ProposedPricePerHour);
    }

    [Fact]
    public async Task Approve_WhenPendingApplication_CreatesSpotAndPromotesUser()
    {
        var applicant = AddUser(17, UserRole.User, "angler17");
        var admin = AddUser(99, UserRole.Admin, "admin99");
        var application = AddApplication(3, applicant.Id, ManagerApplicationStatus.Pending);
        ControllerContextFactory.SetAuthenticatedUser(_controller, admin.Id, Roles.Admin, admin.Username, admin.Email);

        var result = await _controller.Approve(application.Id);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ManagerApplicationDto>(okResult.Value);
        var updatedUser = await _context.Users.SingleAsync(user => user.Id == applicant.Id);
        var spot = await _context.FishingSpots.SingleAsync();

        Assert.Equal(ManagerApplicationStatus.Approved.ToString(), dto.Status);
        Assert.Equal(UserRole.Manager, updatedUser.Role);
        Assert.Equal(applicant.Id, spot.UserId);
        Assert.Equal(applicant.Id, spot.ManagerId);
        await _cacheStore.Received(1).EvictByTagAsync("fishingspots", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_WhenPendingApplication_SetsReason()
    {
        var applicant = AddUser(18, UserRole.User, "angler18");
        var admin = AddUser(100, UserRole.Admin, "admin100");
        var application = AddApplication(4, applicant.Id, ManagerApplicationStatus.Pending);
        ControllerContextFactory.SetAuthenticatedUser(_controller, admin.Id, Roles.Admin, admin.Username, admin.Email);

        var result = await _controller.Reject(application.Id, new RejectManagerApplicationDto
        {
            Reason = "Lipsesc detalii despre administrare."
        });

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<ManagerApplicationDto>(okResult.Value);
        Assert.Equal(ManagerApplicationStatus.Rejected.ToString(), dto.Status);
        Assert.Equal("Lipsesc detalii despre administrare.", dto.RejectionReason);
    }

    private User AddUser(int id, UserRole role, string username)
    {
        var user = new User
        {
            Id = id,
            Username = username,
            Email = $"{username}@test.com",
            PasswordHash = "hash",
            Role = role,
            FirstName = $"First{id}",
            LastName = $"Last{id}"
        };

        _context.Users.Add(user);
        _context.SaveChanges();
        return user;
    }

    private ManagerApplication AddApplication(int id, int applicantUserId, ManagerApplicationStatus status)
    {
        var application = new ManagerApplication
        {
            Id = id,
            ApplicantUserId = applicantUserId,
            LakeName = $"Lake {id}",
            Description = "Description",
            Latitude = 45.0,
            Longitude = 25.0,
            LocationLabel = "Location label",
            ProposedPricePerHour = 20,
            FishSpecies = "[\"Carp\",\"Catfish\"]",
            ContactPhone = "0711111111",
            Motivation = "Motivation",
            AdministrationBasis = "Owner",
            Status = status,
            RejectionReason = status == ManagerApplicationStatus.Rejected ? "Needs more data" : null
        };

        _context.ManagerApplications.Add(application);
        _context.SaveChanges();
        return application;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}