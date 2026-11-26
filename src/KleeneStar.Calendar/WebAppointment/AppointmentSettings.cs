using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The settings of the calendar plugin, read from <c>Plugins:kleenestar.calendar</c>.
    /// </summary>
    /// <remarks>
    /// Every value has a default, so the plugin runs without any settings of its own:
    /// <code>
    /// "Plugins": {
    ///   "kleenestar.calendar": {
    ///     "TimeZone": "Europe/Berlin",
    ///     "AutoCloseInterval": 5,
    ///     "AutoCloseGrace": 0,
    ///     "UpcomingDays": 7,
    ///     "UpcomingCount": 10
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public sealed class AppointmentSettings
    {
        /// <summary>
        /// Gets the zone appointments are planned in when neither the appointment nor the
        /// account names one; <see langword="null"/> for the zone of the host.
        /// </summary>
        public string TimeZone { get; init; }

        /// <summary>
        /// Gets how often, in minutes, the appointments whose time is over are closed. At least
        /// one; zero switches the closing off.
        /// </summary>
        public int AutoCloseInterval { get; init; } = 5;

        /// <summary>
        /// Gets how long, in minutes, an appointment stays open after its last occurrence ended.
        /// </summary>
        public int AutoCloseGrace { get; init; }

        /// <summary>
        /// Gets how many days ahead the sidebar lists the coming appointments.
        /// </summary>
        public int UpcomingDays { get; init; } = 7;

        /// <summary>
        /// Gets how many coming appointments the sidebar lists at most.
        /// </summary>
        public int UpcomingCount { get; init; } = 10;

        /// <summary>
        /// Reads the settings.
        /// </summary>
        /// <param name="configuration">The section of the plugin, or <see langword="null"/>.</param>
        /// <returns>The settings; the defaults for what is missing or not a number.</returns>
        public static AppointmentSettings From(IConfiguration configuration)
        {
            var defaults = new AppointmentSettings();

            if (configuration is null)
            {
                return defaults;
            }

            return new AppointmentSettings
            {
                TimeZone = string.IsNullOrWhiteSpace(configuration["TimeZone"]) ? null : configuration["TimeZone"].Trim(),
                AutoCloseInterval = Number(configuration["AutoCloseInterval"], defaults.AutoCloseInterval, 0, 24 * 60),
                AutoCloseGrace = Number(configuration["AutoCloseGrace"], defaults.AutoCloseGrace, 0, 7 * 24 * 60),
                UpcomingDays = Number(configuration["UpcomingDays"], defaults.UpcomingDays, 1, 366),
                UpcomingCount = Number(configuration["UpcomingCount"], defaults.UpcomingCount, 1, 100)
            };
        }

        /// <summary>
        /// Reads a whole number within bounds.
        /// </summary>
        /// <param name="value">The configured value.</param>
        /// <param name="fallback">The default.</param>
        /// <param name="min">The smallest value allowed.</param>
        /// <param name="max">The largest value allowed.</param>
        /// <returns>The number.</returns>
        private static int Number(string value, int fallback, int min, int max)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
                ? Math.Clamp(number, min, max)
                : fallback;
        }
    }
}
