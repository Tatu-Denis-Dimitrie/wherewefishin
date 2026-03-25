namespace WhereWeFishin.Core.Enums;

public enum UserRole
{
    User,
    Employee,
    Manager,
    Admin
}

public static class Roles
{
    public const string User = nameof(UserRole.User);
    public const string Employee = nameof(UserRole.Employee);
    public const string Manager = nameof(UserRole.Manager);
    public const string Admin = nameof(UserRole.Admin);

    public const string AdminOrManager = Admin + "," + Manager;
    public const string EmployeeOrManagerOrAdmin = Employee + "," + Manager + "," + Admin;
}
