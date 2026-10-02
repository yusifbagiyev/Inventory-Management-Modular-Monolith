namespace SharedServices.Identity
{
    /// <summary>Admins pass every permission check. Users get only what is granted to their role or account.</summary>
    public static class AllRoles
    {
        public const string Admin = "Admin";
        public const string User = "User";
    }
}
