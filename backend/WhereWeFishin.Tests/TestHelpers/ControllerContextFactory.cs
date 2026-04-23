using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.Tests.TestHelpers;

internal static class ControllerContextFactory
{
    public static void SetAuthenticatedUser(
        ControllerBase controller,
        int userId,
        string role = Roles.User,
        string? username = null,
        string? email = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username ?? $"testuser{userId}"),
            new(ClaimTypes.Email, email ?? $"testuser{userId}@mail.com"),
            new(ClaimTypes.Role, role)
        };

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
    }

    public static void SetAnonymousUser(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
    }
}