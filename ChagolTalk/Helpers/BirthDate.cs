namespace ChagolTalk.Helpers
{
    /// <summary>
    /// Turns what a form gave us into something the database will accept.
    ///
    /// ApplicationUser.DateOfBirth is a "timestamp with time zone" column, and
    /// Npgsql refuses to write a DateTime whose Kind is anything but Utc --
    /// it throws rather than guessing an offset. Model binding an
    /// &lt;input type="date"&gt; produces Kind=Unspecified, so every value
    /// arriving from a form has to be stamped before it is stored. Doing that
    /// in one place keeps the three entry points (registration, guest quick
    /// start, profile edit) from each having to remember.
    /// </summary>
    public static class BirthDate
    {
        /// <summary>
        /// A year of birth as a stored date. 1 January by convention -- the
        /// oldest that year can be, matching how <see cref="MinimumAgeAttribute"/>
        /// reads a bare year when it validates one.
        /// </summary>
        public static DateTime FromYear(int year) =>
            new(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// A date from a form, stamped as UTC. The time of day is dropped: a
        /// birth date has no meaningful clock time, and keeping one would make
        /// the same birthday compare unequal to itself.
        /// </summary>
        public static DateTime Normalise(DateTime value) =>
            DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

        /// <inheritdoc cref="Normalise(DateTime)"/>
        public static DateTime? Normalise(DateTime? value) =>
            value.HasValue ? Normalise(value.Value) : null;
    }
}
