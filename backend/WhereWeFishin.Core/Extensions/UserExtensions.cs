namespace WhereWeFishin.Core.Extensions;

public static class UserExtensions
{
    public static string GetDisplayName(string? firstName, string? lastName, string username)
    {
        var fullName = $"{firstName} {lastName}".Trim();
        return fullName.Length > 0 ? fullName : username;
    }
}
