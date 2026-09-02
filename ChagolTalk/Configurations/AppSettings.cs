namespace ChagolTalk.Configurations
{
    public static class AppSettings
    {
        public const string AppName = "ChagolTalk";

        public const string Tagline =
            "One click. One stranger. One conversation.";

        public const string Version = "v0.1 Alpha";

        public const string Company = "ChagolTalk";

        public const string Theme = "Dark";

        /// <summary>
        /// Minimum age to hold an account. Referenced from the view models so
        /// registration, the guest quick start and profile editing can never
        /// drift apart on the number. Must stay a const: attribute arguments
        /// are baked in at compile time.
        /// </summary>
        public const int MinimumAge = 13;

        /// <summary>
        /// Address shown in the privacy policy for account and data removal
        /// requests. MUST be filled in: while it is empty the policy promises a
        /// way to reach us without naming one.
        /// </summary>
        public const string ContactEmail = "";
    }
}
