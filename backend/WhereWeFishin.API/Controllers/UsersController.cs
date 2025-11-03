using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.DTOs;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Interfaces;

namespace WhereWeFishin.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IUnitOfWork unitOfWork, ILogger<UsersController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _unitOfWork.Users.GetAllAsync();
        var userDtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FirstName = u.FirstName,
            LastName = u.LastName,
            ProfilePictureUrl = u.ProfilePictureUrl,
            CreatedAt = u.CreatedAt
        });

        return Ok(userDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(Guid id)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var userDto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt
        };

        return Ok(userDto);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto createUserDto)
    {
        // Note: In a real application, you should hash the password here
        var user = new User
        {
            Username = createUserDto.Username,
            Email = createUserDto.Email,
            PasswordHash = createUserDto.Password, // TODO: Hash password
            FirstName = createUserDto.FirstName,
            LastName = createUserDto.LastName
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();

        var userDto = new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            ProfilePictureUrl = user.ProfilePictureUrl,
            CreatedAt = user.CreatedAt
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserDto updateUserDto)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (updateUserDto.FirstName != null) user.FirstName = updateUserDto.FirstName;
        if (updateUserDto.LastName != null) user.LastName = updateUserDto.LastName;
        if (updateUserDto.ProfilePictureUrl != null) user.ProfilePictureUrl = updateUserDto.ProfilePictureUrl;

        await _unitOfWork.Users.UpdateAsync(user);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var exists = await _unitOfWork.Users.ExistsAsync(id);
        if (!exists)
        {
            return NotFound();
        }

        await _unitOfWork.Users.DeleteAsync(id);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }
}
