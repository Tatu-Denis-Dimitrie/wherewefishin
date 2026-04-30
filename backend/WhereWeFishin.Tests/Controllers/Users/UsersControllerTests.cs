using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;
using WhereWeFishin.Core.Interfaces;
using WhereWeFishin.Tests.TestHelpers;

namespace WhereWeFishin.Tests.Controllers;

public class UsersControllerTests
{
    private readonly IRepository<User> _userRepository;
    private readonly IAuthService _authService;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _userRepository = Substitute.For<IRepository<User>>();
        _authService = Substitute.For<IAuthService>();
        _controller = new UsersController(_userRepository, _authService);
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 1);
    }

    private static User CreateUser(int id, UserRole role = UserRole.User) => new()
    {
        Id = id,
        Username = $"user{id}",
        Email = $"user{id}@test.com",
        FirstName = $"First{id}",
        LastName = $"Last{id}",
        PasswordHash = "hash123",
        Role = role
    };

    [Fact]
    public async Task GetUsers_ReturnsAllUsers()
    {
        // Arrange
        var users = new List<User>
        {
            new User { Id = 1, Username = "user1", Email = "user1@test.com", FirstName = "John", LastName = "Doe" },
            new User { Id = 2, Username = "user2", Email = "user2@test.com", FirstName = "Jane", LastName = "Smith" }
        };
        _userRepository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(users);

        // Act
        var result = await _controller.GetUsers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUsers = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value);
        Assert.Equal(2, returnedUsers.Count());
    }

    [Fact]
    public async Task GetUser_WithValidId_ReturnsUser()
    {
        // Arrange
        var user = new User 
        { 
            Id = 1, 
            Username = "testuser", 
            Email = "test@test.com", 
            FirstName = "John", 
            LastName = "Doe" 
        };
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _controller.GetUser(1);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var returnedUser = Assert.IsType<UserDto>(okResult.Value);
        Assert.Equal("testuser", returnedUser.Username);
        Assert.Equal("test@test.com", returnedUser.Email);
    }

    [Fact]
    public async Task GetManagers_ReturnsOnlyManagers()
    {
        // Arrange
        _userRepository.UseInMemoryStore(new[]
        {
            CreateUser(1, UserRole.Manager),
            CreateUser(2, UserRole.User),
            CreateUser(3, UserRole.Manager)
        });

        // Act
        var result = await _controller.GetManagers();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var managers = Assert.IsAssignableFrom<IEnumerable<UserDto>>(okResult.Value).ToList();
        Assert.Equal(2, managers.Count);
        Assert.All(managers, manager => Assert.Equal(Roles.Manager, manager.Role));
        Assert.All(managers, manager => Assert.True(manager.IsActive));
    }

    [Fact]
    public async Task GetUser_WhenCallerIsDifferentAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 5);

        // Act
        var result = await _controller.GetUser(1);

        // Assert
        Assert.IsType<ForbidResult>(result.Result);
    }

    [Fact]
    public async Task GetUser_AsAdmin_CanAccessAnotherUser()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 99, role: Roles.Admin);
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateUser(1));

        // Act
        var result = await _controller.GetUser(1);

        // Assert
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetUser_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var result = await _controller.GetUser(1);

        // Assert
        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateUser_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var existingUser = new User 
        { 
            Id = 1, 
            Username = "olduser", 
            Email = "old@test.com",
            PasswordHash = "hash123"
        };
        var updateDto = new UpdateUserDto 
        { 
            FirstName = "John",
            LastName = "Doe"
        };

        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existingUser);

        // Act
        var result = await _controller.UpdateUser(1, updateDto);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _userRepository.Received(1).UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateUser_WhenCallerIsDifferentAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 5);

        // Act
        var result = await _controller.UpdateUser(1, new UpdateUserDto { FirstName = "Blocked" });

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task UpdateUser_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var result = await _controller.UpdateUser(1, new UpdateUserDto { FirstName = "Missing" });

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task UpdateUser_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var existingUser = CreateUser(1);
        existingUser.ProfilePictureUrl = "old-picture.png";
        _userRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(existingUser);

        // Act
        await _controller.UpdateUser(1, new UpdateUserDto { FirstName = "Updated" });

        // Assert
        await _userRepository.Received(1).UpdateAsync(
            Arg.Is<User>(user =>
                user.FirstName == "Updated" &&
                user.LastName == existingUser.LastName &&
                user.ProfilePictureUrl == "old-picture.png"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        _controller.ModelState.AddModelError(nameof(ChangePasswordRequest.NewPassword), "Required");

        // Act
        var result = await _controller.ChangePassword(1, new ChangePasswordRequest());

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        await _authService.DidNotReceive().ChangePasswordAsync(Arg.Any<int>(), Arg.Any<ChangePasswordRequest>());
    }

    [Fact]
    public async Task ChangePassword_WhenCallerIsDifferentAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 5);

        // Act
        var result = await _controller.ChangePassword(1, new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ChangePassword_WhenServiceSucceeds_ReturnsNoContent()
    {
        // Arrange
        _authService.ChangePasswordAsync(1, Arg.Any<ChangePasswordRequest>()).Returns(true);

        // Act
        var result = await _controller.ChangePassword(1, new ChangePasswordRequest
        {
            CurrentPassword = "oldpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task ChangePassword_WhenServiceFails_ReturnsBadRequest()
    {
        // Arrange
        _authService.ChangePasswordAsync(1, Arg.Any<ChangePasswordRequest>()).Returns(false);

        // Act
        var result = await _controller.ChangePassword(1, new ChangePasswordRequest
        {
            CurrentPassword = "wrongpassword",
            NewPassword = "newpassword123"
        });

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WithValidId_ReturnsNoContent()
    {
        // Arrange
        _userRepository.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var result = await _controller.DeleteUser(1);

        // Assert
        Assert.IsType<NoContentResult>(result);
        await _userRepository.Received(1).DeleteAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteUser_WhenCallerIsDifferentAndNotAdmin_ReturnsForbid()
    {
        // Arrange
        ControllerContextFactory.SetAuthenticatedUser(_controller, userId: 5);

        // Act
        var result = await _controller.DeleteUser(1);

        // Assert
        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task DeleteUser_WhenMissing_ReturnsNotFound()
    {
        // Arrange
        _userRepository.ExistsAsync(1, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var result = await _controller.DeleteUser(1);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        await _userRepository.DidNotReceive().DeleteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

}

