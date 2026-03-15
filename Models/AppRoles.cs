using System.Collections.Generic;

namespace InvenTrack.Models
{
    public static class AppRoles
    {
        public const string Admin = "Admin";
        public const string RegionalManager = "RegionalManager";
        public const string Manager = "Manager";
        public const string Supervisor = "Supervisor";
        public const string Employee = "Employee";

        public static readonly string[] All =
        {
            Admin,
            RegionalManager,
            Manager,
            Supervisor,
            Employee
        };

        public static readonly HashSet<string> GlobalRoles = new()
        {
            Admin,
            RegionalManager
        };

        public static readonly HashSet<string> ScopedRoles = new()
        {
            Manager,
            Supervisor,
            Employee
        };
    }
}