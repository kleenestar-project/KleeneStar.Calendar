using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// One occurrence of an appointment: a single appointment has one, a series one per
    /// repetition.
    /// </summary>
    /// <param name="ObjectId">The appointment object.</param>
    /// <param name="Start">The start in UTC.</param>
    /// <param name="End">The end in UTC.</param>
    /// <param name="AllDay">Whether the occurrence covers whole days.</param>
    /// <param name="Recurring">Whether the occurrence belongs to a series.</param>
    public sealed record AppointmentOccurrence(Guid ObjectId, DateTime Start, DateTime End, bool AllDay, bool Recurring)
    {
        /// <summary>
        /// Gets the identifier of the occurrence: the object id for a single appointment, the
        /// object id and the start for an occurrence of a series.
        /// </summary>
        public string Id => Recurring
            ? $"{ObjectId}@{AppointmentSchedule.FormatInstant(Start)}"
            : ObjectId.ToString();

        /// <summary>
        /// Determines whether the occurrence overlaps a period. An occurrence that ends where
        /// the period starts does not overlap it; one without duration overlaps the period it
        /// lies in.
        /// </summary>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <returns><see langword="true"/> when the two overlap.</returns>
        public bool Overlaps(DateTime from, DateTime to)
        {
            return End == Start
                ? Start >= from && Start < to
                : Start < to && End > from;
        }
    }

    /// <summary>
    /// Answers the occurrences of a schedule and keeps the derived columns of a schedule in step
    /// with what it says.
    /// </summary>
    public static class AppointmentSchedule
    {
        /// <summary>
        /// The format an occurrence start is written in, in the exceptions of a series and in
        /// the identifier of an occurrence.
        /// </summary>
        public const string InstantFormat = "yyyyMMdd'T'HHmmss'Z'";

        /// <summary>
        /// Writes an instant in <see cref="InstantFormat"/>.
        /// </summary>
        /// <param name="utc">The instant in UTC.</param>
        /// <returns>The text.</returns>
        public static string FormatInstant(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString(InstantFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads an instant written in <see cref="InstantFormat"/>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The instant in UTC, or <see langword="null"/>.</returns>
        public static DateTime? ParseInstant(string text)
        {
            return DateTime.TryParseExact((text ?? string.Empty).Trim(), InstantFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
                ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
                : null;
        }

        /// <summary>
        /// Reads the exceptions of a series.
        /// </summary>
        /// <param name="appointment">The schedule.</param>
        /// <returns>The starts of the removed occurrences, in UTC.</returns>
        public static ISet<DateTime> Exceptions(AppointmentEntity appointment)
        {
            return (appointment?.Exceptions ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(ParseInstant)
                .Where(x => x is not null)
                .Select(x => x.Value)
                .ToHashSet();
        }

        /// <summary>
        /// Writes the exceptions of a series.
        /// </summary>
        /// <param name="exceptions">The starts of the removed occurrences, in UTC.</param>
        /// <returns>The text, or <see langword="null"/> for none.</returns>
        public static string FormatExceptions(IEnumerable<DateTime> exceptions)
        {
            var list = (exceptions ?? []).Distinct().OrderBy(x => x).Select(FormatInstant).ToList();

            return list.Count == 0 ? null : string.Join(";", list);
        }

        /// <summary>
        /// Answers the occurrences of a schedule that overlap a period, in ascending order.
        /// </summary>
        /// <param name="appointment">The schedule.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <param name="limit">The most occurrences answered.</param>
        /// <returns>The occurrences.</returns>
        public static IEnumerable<AppointmentOccurrence> Occurrences(AppointmentEntity appointment, DateTime from, DateTime to, int limit = 1000)
        {
            if (appointment is null || to <= from)
            {
                yield break;
            }

            var duration = appointment.End - appointment.Start;

            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            var rule = RecurrenceRule.Parse(appointment.Recurrence);

            if (rule is null)
            {
                var single = new AppointmentOccurrence(appointment.ObjectId, appointment.Start, appointment.Start + duration, appointment.AllDay, false);

                if (single.Overlaps(from, to))
                {
                    yield return single;
                }

                yield break;
            }

            var zone = AppointmentTime.Zone(appointment.TimeZone);
            var exceptions = Exceptions(appointment);
            var localStart = AppointmentTime.ToLocal(appointment.Start, zone);
            var localDuration = AppointmentTime.ToLocal(appointment.Start + duration, zone) - localStart;
            var windowEnd = AppointmentTime.ToLocal(to, zone).AddDays(1);
            var count = 0;

            foreach (var local in rule.Expand(localStart, x => AppointmentTime.ToUtc(x, zone), windowEnd))
            {
                var start = AppointmentTime.ToUtc(local, zone);

                // an all-day occurrence keeps its whole days in local time, a timed one its length
                var end = appointment.AllDay
                    ? AppointmentTime.ToUtc(local + localDuration, zone)
                    : start + duration;

                if (exceptions.Contains(start))
                {
                    continue;
                }

                var occurrence = new AppointmentOccurrence(appointment.ObjectId, start, end, appointment.AllDay, true);

                if (start >= to)
                {
                    yield break;
                }

                if (occurrence.Overlaps(from, to))
                {
                    yield return occurrence;

                    if (++count >= limit)
                    {
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Answers every occurrence of a bounded series, or the first ones of an open series.
        /// </summary>
        /// <param name="appointment">The schedule.</param>
        /// <param name="limit">The most occurrences answered.</param>
        /// <returns>The occurrences.</returns>
        public static IEnumerable<AppointmentOccurrence> AllOccurrences(AppointmentEntity appointment, int limit)
        {
            return Occurrences(appointment, appointment.Start.AddTicks(-1), DateTime.MaxValue.AddYears(-1), limit);
        }

        /// <summary>
        /// Computes the end of the last occurrence of a schedule (<see cref="AppointmentEntity.SeriesEnd"/>).
        /// </summary>
        /// <param name="appointment">The schedule.</param>
        /// <returns>The end in UTC, or <see langword="null"/> for a series without end.</returns>
        public static DateTime? ComputeSeriesEnd(AppointmentEntity appointment)
        {
            if (appointment is null)
            {
                return null;
            }

            var rule = RecurrenceRule.Parse(appointment.Recurrence);

            if (rule is null)
            {
                return appointment.End < appointment.Start ? appointment.Start : appointment.End;
            }

            if (!rule.IsBounded)
            {
                return null;
            }

            var last = AllOccurrences(appointment, RecurrenceRule.MaxOccurrences).LastOrDefault();

            // a series whose every occurrence was removed ends where it began
            return last?.End ?? appointment.End;
        }

        /// <summary>
        /// Brings the derived columns of a schedule in step: the end is never before the start,
        /// an all-day schedule covers whole local days, an empty rule is none, and the end of the
        /// series is computed again.
        /// </summary>
        /// <param name="appointment">The schedule.</param>
        public static void Normalize(AppointmentEntity appointment)
        {
            if (appointment is null)
            {
                return;
            }

            appointment.Start = DateTime.SpecifyKind(appointment.Start, DateTimeKind.Utc);
            appointment.End = DateTime.SpecifyKind(appointment.End, DateTimeKind.Utc);

            if (appointment.AllDay)
            {
                var zone = AppointmentTime.Zone(appointment.TimeZone);
                var firstDay = AppointmentTime.ToLocal(appointment.Start, zone).Date;
                var lastDay = AppointmentTime.ToLocal(appointment.End, zone);

                // an end at midnight closes the day before it
                var afterLast = lastDay.TimeOfDay == TimeSpan.Zero && lastDay.Date > firstDay
                    ? lastDay.Date
                    : lastDay.Date.AddDays(1);

                appointment.Start = AppointmentTime.ToUtc(firstDay, zone);
                appointment.End = AppointmentTime.ToUtc(afterLast, zone);
            }

            if (appointment.End < appointment.Start)
            {
                appointment.End = appointment.Start;
            }

            if (string.IsNullOrWhiteSpace(appointment.Recurrence))
            {
                appointment.Recurrence = null;
                appointment.Exceptions = null;
            }
            else
            {
                var rule = RecurrenceRule.Parse(appointment.Recurrence);
                appointment.Recurrence = rule?.ToString();
            }

            appointment.Location = string.IsNullOrWhiteSpace(appointment.Location) ? null : appointment.Location.Trim();
            appointment.SeriesEnd = ComputeSeriesEnd(appointment);
        }
    }
}
