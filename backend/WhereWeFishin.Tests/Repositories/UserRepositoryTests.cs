using Microsoft.EntityFrameworkCore;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Database.Context;
using WhereWeFishin.Database.Repositories;

namespace WhereWeFishin.Tests.Repositories;

public class UserRepositoryTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Repository<User> _repository;

    public UserRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _repository = new Repository<User>(_context);
    }

    [Fact]
    public async Task AddAsync_AddsUserToDatabase()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash123",
            FirstName = "John",
            LastName = "Doe"
        };

        // Act
        var result = await _repository.AddAsync(user);
        await _context.SaveChangesAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
        Assert.True(result.Id > 0);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectUser()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash123"
        };
        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("testuser", result.Username);
        Assert.Equal("test@test.com", result.Email);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllUsers()
    {
        // Arrange
        var users = new[]
        {
            new User { Username = "user1", Email = "user1@test.com", PasswordHash = "hash1" },
            new User { Username = "user2", Email = "user2@test.com", PasswordHash = "hash2" }
        };

        foreach (var user in users)
        {
            await _repository.AddAsync(user);
        }
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetAllAsync();

        // Assert
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task UpdateAsync_UpdatesUserInDatabase()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash123",
            FirstName = "John"
        };
        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        user.FirstName = "Jane";
        user.Email = "newemail@test.com";
        await _repository.UpdateAsync(user);
        await _context.SaveChangesAsync();

        // Assert
        var updatedUser = await _repository.GetByIdAsync(user.Id);
        Assert.NotNull(updatedUser);
        Assert.Equal("Jane", updatedUser.FirstName);
        Assert.Equal("newemail@test.com", updatedUser.Email);
    }

    [Fact]
    public async Task DeleteAsync_RemovesUserFromDatabase()
    {
        // Arrange
        var user = new User
        {
            Username = "testuser",
            Email = "test@test.com",
            PasswordHash = "hash123"
        };
        await _repository.AddAsync(user);
        await _context.SaveChangesAsync();
        var userId = user.Id;

        // Act
        await _repository.DeleteAsync(userId);
        await _context.SaveChangesAsync();

        // Assert
        var deletedUser = await _repository.GetByIdAsync(userId);
        Assert.Null(deletedUser);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
