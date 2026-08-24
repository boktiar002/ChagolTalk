namespace ChagolTalk.Models.Enums
{
    /// <summary>
    /// What kind of conversation a user is looking for.
    /// Voice is the primary experience; Text is the fallback.
    /// </summary>
    public enum ChatMode
    {
        /// <summary>Match with anyone, voice preferred.</summary>
        Any = 0,

        /// <summary>Only match with people who also want to talk.</summary>
        Voice = 1,

        /// <summary>Only match with people who want to type.</summary>
        Text = 2
    }
}
