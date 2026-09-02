using System.ComponentModel.DataAnnotations;
using ChagolTalk.Configurations;

namespace ChagolTalk.Helpers
{
    /// <summary>
    /// Validates that someone is at least <see cref="Years"/> years old.
    ///
    /// ChagolTalk puts people into live voice calls with strangers, so the age
    /// question belongs at the door -- on registration and on the guest quick
    /// start -- and not only on a profile page a user never has to open.
    ///
    /// Deliberately a date or year entry rather than an "I am old enough"
    /// tickbox: a checkbox records only that someone wanted to get in, while a
    /// date is a neutral question that can be stored and re-checked later.
    ///
    /// Accepts either a full <see cref="DateTime"/> or an <see cref="int"/>
    /// year of birth. The guest quick start asks only for a year, because a
    /// date picker on a phone opens on today and makes a grown adult scroll
    /// back two decades before they can start a call. A year is read as
    /// 1 January -- the oldest that year can be, and the same convention the
    /// controller uses when it stores the value -- so someone whose birthday
    /// falls late in the year is admitted on the strength of the year alone.
    /// That is the accepted cost of asking one question instead of three.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class MinimumAgeAttribute : ValidationAttribute
    {
        /// <summary>Oldest year we will accept, to catch typos like "19".</summary>
        private const int EarliestPlausibleYear = 1900;

        public int Years { get; }

        public MinimumAgeAttribute(int years)
        {
            Years = years;
            ErrorMessage = $"You must be at least {years} years old to use {AppSettings.AppName}.";
        }

        public override bool IsValid(object? value)
        {
            // Whether a missing value is acceptable is [Required]'s business,
            // not this attribute's -- EditProfile leaves it optional.
            if (value is null)
                return true;

            var today = DateTime.UtcNow.Date;
            DateTime birthDate;

            switch (value)
            {
                case int year:
                    if (year < EarliestPlausibleYear || year > today.Year)
                        return false;

                    birthDate = new DateTime(year, 1, 1);
                    break;

                case DateTime date:
                    birthDate = date.Date;
                    break;

                default:
                    return false;
            }

            // A future date is not a young user, it is a bad value.
            if (birthDate > today)
                return false;

            var age = today.Year - birthDate.Year;

            // Only count this year's birthday once it has actually passed.
            if (birthDate > today.AddYears(-age))
                age--;

            return age >= Years;
        }
    }
}
