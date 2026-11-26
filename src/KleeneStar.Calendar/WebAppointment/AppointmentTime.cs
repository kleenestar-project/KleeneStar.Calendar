using System;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// Converts between the UTC instants an appointment is stored in and the local times it
    /// is planned and shown in.
    /// </summary>
    /// <remarks>
    /// A zone is named by a .NET or an IANA id; both are understood on every platform the host
    /// runs on. An unknown or empty id is the zone of the host - an appointment planned before a
    /// zone was chosen keeps the times it was shown with.
    /// <para>
    /// A local time the clocks skip (the hour lost to daylight saving time) does not exist and
    /// is moved forward by the gap; a local time the clocks repeat is read as the earlier one,
    /// which is what a person writing "02:30" in the night of the change means.
    /// </para>
    /// </remarks>
    public static class AppointmentTime
    {
        /// <summary>
        /// Resolves a time zone.
        /// </summary>
        /// <param name="id">The .NET or IANA id, or <see langword="null"/>.</param>
        /// <returns>The zone; the host's for an empty or unknown id.</returns>
        public static TimeZoneInfo Zone(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return TimeZoneInfo.Local;
            }

            if (string.Equals(id.Trim(), "UTC", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id.Trim(), "Etc/UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.Utc;
            }

            if (TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out var zone))
            {
                return zone;
            }

            if (TimeZoneInfo.TryConvertIanaIdToWindowsId(id.Trim(), out var windowsId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(windowsId, out zone))
            {
                return zone;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(id.Trim(), out var ianaId)
                && TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out zone))
            {
                return zone;
            }

            return TimeZoneInfo.Local;
        }

        /// <summary>
        /// Converts a local time of a zone to UTC.
        /// </summary>
        /// <param name="local">The local time (its kind is ignored).</param>
        /// <param name="zone">The zone.</param>
        /// <returns>The instant in UTC.</returns>
        public static DateTime ToUtc(DateTime local, TimeZoneInfo zone)
        {
            zone ??= TimeZoneInfo.Local;

            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

            if (zone.IsInvalidTime(unspecified))
            {
                // the clocks skip this time: it is read with the offset in force before the
                // jump, which moves it forward by the gap (02:30 becomes 03:30)
                var before = zone.GetUtcOffset(unspecified.AddHours(-6));

                return DateTime.SpecifyKind(unspecified - before, DateTimeKind.Utc);
            }

            if (zone.IsAmbiguousTime(unspecified))
            {
                var offsets = zone.GetAmbiguousTimeOffsets(unspecified);
                var earlier = offsets[0] > offsets[^1] ? offsets[0] : offsets[^1];

                return DateTime.SpecifyKind(unspecified - earlier, DateTimeKind.Utc);
            }

            return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        }

        /// <summary>
        /// Converts an instant to the local time of a zone.
        /// </summary>
        /// <param name="utc">The instant (a local or unspecified kind is taken as UTC).</param>
        /// <param name="zone">The zone.</param>
        /// <returns>The local time, of unspecified kind.</returns>
        public static DateTime ToLocal(DateTime utc, TimeZoneInfo zone)
        {
            zone ??= TimeZoneInfo.Local;

            var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(instant, zone), DateTimeKind.Unspecified);
        }
    }
}
