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

        /// <summary>Single TURN URL. Ignored when Urls is set.</summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Multiple TURN URLs sharing one username/credential (e.g. Metered
        /// gives you turn:.../udp, turn:.../tcp and turns:.../443 for a
        /// single account) -- semicolon separated so it fits one env var.
        /// </summary>
        public string Urls { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Credential { get; set; } = string.Empty;
    }
}
