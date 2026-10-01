namespace SharedServices.Identity
{
    /// <summary>
    /// Password rules shared by the identity options and every form or DTO that sets a password.
    /// Applies when a password is set: existing shorter passwords keep working until changed.
    /// </summary>
    public static class PasswordRules
    {
        /// <summary>The app is on the internet; 6 allowed passwords such as "Baku2025".</summary>
        public const int MinLength = 10;

        /// <summary>English text key (az.json) of the length rule.</summary>
        public const string LengthMessage = "Password must be at least 10 characters long, with an uppercase letter, a lowercase letter and a digit.";

        /// <summary>Longest username / password a sign-in accepts, so a failed attempt cannot write megabytes to the logs.</summary>
        public const int MaxUsernameLength = 256;
        public const int MaxPasswordLength = 512;
    }
}
