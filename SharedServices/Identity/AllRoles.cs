namespace SharedServices.Identity
{
    /// <summary>Admins pass every check, users only get what their role or account is granted.</summary>
    public static class AllRoles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }
}
