namespace ChagolTalk.Options
{
    /// <summary>
    /// Credentials for an optional TURN server. Without TURN, WebRTC voice
    /// calls fail for anyone behind a symmetric NAT or a restrictive
    /// firewall (common on mobile carriers) -- STUN alone is not enough to
    /// reliably connect two strangers in production.
    ///
    /// Populate these from configuration/environment variables. When Url is
    /// empty the app falls back to Google's public STUN server only.
    /// </summary>
    public class TurnServerOptions
    {
        public const string SectionName = "TurnServer";

        /// <summary>
        /// One or more TURN URLs sharing a username/credential, semicolon
        /// separated so the whole set fits in a single environment variable.
        /// Providers typically hand out several endpoints for one account
        /// (UDP, TCP, and TLS on 443) and offering all of them meaningfully
        /// improves the odds of getting through a restrictive firewall.
        /// </summary>
        public string Urls { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Credential { get; set; } = string.Empty;
    }
}
