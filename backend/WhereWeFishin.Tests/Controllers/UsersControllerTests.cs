using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WhereWeFishin.API.Controllers;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

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
    }

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

}

