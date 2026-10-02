namespace SharedServices.Identity
{
    /// <summary>Password rules checked only when a password is set, so existing shorter ones keep working until changed.</summary>
    public static class PasswordRules
    {
        /// <summary>The site is public, so short guessable passwords are not allowed.</summary>
        public const int MinLength = 10;

        /// <summary>Also the translation key in the language files.</summary>
        public const string LengthMessage = "Password must be at least 10 characters long, with an uppercase letter, a lowercase letter and a digit.";

        /// <summary>Caps sign-in input so a failed attempt cannot write megabytes to the logs.</summary>
        public const int MaxUsernameLength = 256;
        public const int MaxPasswordLength = 512;
    }
}
