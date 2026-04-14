using System.Security.Claims;
using WhereWeFishin.Core.Entities;
using WhereWeFishin.Core.Enums;

namespace WhereWeFishin.API.Extensions;

public static class SpotAuthorizationExtensions
{
    /// <summary>
    /// Checks if the current user is authorized to manage a fishing spot.
    /// Returns true if: Admin, or spot's ManagerId matches, or spot's UserId matches.
    /// </summary>
    public static bool CanManageSpot(this ClaimsPrincipal user, FishingSpot spot)
    {
        if (user.IsInRole(Roles.Admin))
            return true;

        var userId = user.GetUserId();
        return userId.HasValue && (spot.ManagerId == userId.Value || spot.UserId == userId.Value);
    }
}
