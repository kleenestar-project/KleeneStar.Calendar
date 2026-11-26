using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The frequency a series repeats with (<c>FREQ</c> of RFC 5545).
    /// </summary>
    public enum RecurrenceFrequency
    {
        /// <summary>
        /// Every day (or every n-th day).
        /// </summary>
        Daily,

        /// <summary>
        /// Every week (or every n-th week), on one or several weekdays.
        /// </summary>
        Weekly,

        /// <summary>
        /// Every month (or every n-th month), on a day of the month or an n-th weekday.
        /// </summary>
        Monthly,

        /// <summary>
        /// Every year (or every n-th year).
        /// </summary>
        Yearly
    }

    /// <summary>
    /// A weekday of a rule, optionally with its position in the month or year
    /// (<c>MO</c>, <c>2TU</c>, <c>-1FR</c>).
    /// </summary>
    /// <param name="Day">The weekday.</param>
    /// <param name="Ordinal">The position (1 the first, -1 the last), or 0 for every such day.</param>
    public readonly record struct RecurrenceWeekday(DayOfWeek Day, int Ordinal = 0);

    /// <summary>
    /// The rule a series of appointments repeats by: the subset of the recurrence rule of
    /// RFC 5545 that calendars write for meetings - a frequency with an interval, the weekdays,
    /// days of the month and months it falls on, and an end by count or date.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule works in local time. <see cref="Expand"/> takes the local start of the first
    /// occurrence and answers local starts, so a meeting at ten stays at ten when the clocks
    /// change; the caller converts from and to UTC in the appointment's time zone.
    /// </para>
    /// <para>
    /// The first occurrence is the start of the appointment itself, as every calendar shows it,
    /// and it counts towards <see cref="Count"/>. A date a rule names that does not exist - the
    /// 31st in a short month, the 29th of February outside a leap year - is skipped, as
    /// RFC 5545 says. Parts the subset does not know (<c>BYSETPOS</c>, <c>BYHOUR</c>, …) make
    /// <see cref="TryParse"/> fail rather than repeat differently than written.
    /// </para>
    /// </remarks>
    public sealed class RecurrenceRule
    {
        /// <summary>
        /// The most occurrences a series is expanded to, whatever the rule says: a guard
        /// against a rule without end being walked to the end of time.
        /// </summary>
        public const int MaxOccurrences = 5000;

        /// <summary>
        /// The tokens of the weekdays in the notation of RFC 5545, Monday first.
        /// </summary>
        private static readonly (string Token, DayOfWeek Day)[] _days =
        [
            ("MO", DayOfWeek.Monday),
            ("TU", DayOfWeek.Tuesday),
            ("WE", DayOfWeek.Wednesday),
            ("TH", DayOfWeek.Thursday),
            ("FR", DayOfWeek.Friday),
            ("SA", DayOfWeek.Saturday),
            ("SU", DayOfWeek.Sunday)
        ];

        /// <summary>
        /// Gets the frequency.
        /// </summary>
        public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Weekly;

        /// <summary>
        /// Gets the interval: every n-th day, week, month or year. At least 1.
        /// </summary>
        public int Interval { get; init; } = 1;

        /// <summary>
        /// Gets the number of occurrences, counting the first, or <see langword="null"/>.
        /// </summary>
        public int? Count { get; init; }

        /// <summary>
        /// Gets the last moment an occurrence may start at, in UTC, or <see langword="null"/>.
        /// </summary>
        public DateTime? Until { get; init; }

        /// <summary>
        /// Gets the weekdays the series falls on (<c>BYDAY</c>).
        /// </summary>
        public IReadOnlyList<RecurrenceWeekday> ByDay { get; init; } = [];

        /// <summary>
        /// Gets the days of the month the series falls on (<c>BYMONTHDAY</c>, negative from the
        /// end of the month).
        /// </summary>
        public IReadOnlyList<int> ByMonthDay { get; init; } = [];

        /// <summary>
        /// Gets the months the series falls in (<c>BYMONTH</c>, 1 to 12).
        /// </summary>
        public IReadOnlyList<int> ByMonth { get; init; } = [];

        /// <summary>
        /// Gets whether the series has an end - a count or a last date.
        /// </summary>
        public bool IsBounded => Count is not null || Until is not null;

        /// <summary>
        /// Parses a rule written in the notation of RFC 5545. A leading <c>RRULE:</c> is allowed.
        /// </summary>
        /// <param name="text">The rule.</param>
        /// <param name="rule">The parsed rule.</param>
        /// <param name="error">Why the rule was refused.</param>
        /// <returns><see langword="true"/> when the rule was read.</returns>
        public static bool TryParse(string text, out RecurrenceRule rule, out string error)
        {
            rule = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "The rule is empty.";
                return false;
            }

            var body = text.Trim();

            if (body.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase))
            {
                body = body[6..];
            }

            RecurrenceFrequency? frequency = null;
            var interval = 1;
            int? count = null;
            DateTime? until = null;
            var byDay = new List<RecurrenceWeekday>();
            var byMonthDay = new List<int>();
            var byMonth = new List<int>();

            foreach (var part in body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separator = part.IndexOf('=');

                if (separator <= 0)
                {
                    error = $"The part '{part}' is not NAME=VALUE.";
                    return false;
                }

                var name = part[..separator].Trim().ToUpperInvariant();
                var value = part[(separator + 1)..].Trim();

                switch (name)
                {
                    case "FREQ":
                        frequency = value.ToUpperInvariant() switch
                        {
                            "DAILY" => RecurrenceFrequency.Daily,
                            "WEEKLY" => RecurrenceFrequency.Weekly,
                            "MONTHLY" => RecurrenceFrequency.Monthly,
                            "YEARLY" => RecurrenceFrequency.Yearly,
                            _ => null
                        };

                        if (frequency is null)
                        {
                            error = $"The frequency '{value}' is not supported.";
                            return false;
                        }

                        break;

                    case "INTERVAL":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out interval) || interval < 1 || interval > 1000)
                        {
                            error = $"The interval '{value}' is not a number from 1 to 1000.";
                            return false;
                        }

                        break;

                    case "COUNT":
                        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount) || parsedCount < 1)
                        {
                            error = $"The count '{value}' is not a positive number.";
                            return false;
                        }

                        count = Math.Min(parsedCount, MaxOccurrences);
                        break;

                    case "UNTIL":
                        until = ParseUntil(value);

                        if (until is null)
                        {
                            error = $"The date '{value}' is not a date of RFC 5545.";
                            return false;
                        }

                        break;

                    case "BYDAY":
                        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (!TryParseWeekday(token, out var weekday))
                            {
                                error = $"The weekday '{token}' is not known.";
                                return false;
                            }

                            byDay.Add(weekday);
                        }

                        break;

                    case "BYMONTHDAY":
                        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) || day == 0 || day < -31 || day > 31)
                            {
                                error = $"The day of the month '{token}' is not from 1 to 31.";
                                return false;
                            }

                            byMonthDay.Add(day);
                        }

                        break;

                    case "BYMONTH":
                        foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var month) || month < 1 || month > 12)
                            {
                                error = $"The month '{token}' is not from 1 to 12.";
                                return false;
                            }

                            byMonth.Add(month);
                        }

                        break;

                    case "WKST":
                        // the weeks of a weekly series start on Monday, as they do in ISO 8601;
                        // another start would only change series with an interval above one
                        break;

                    default:
                        error = $"The part '{name}' is not supported.";
                        return false;
                }
            }

            if (frequency is null)
            {
                error = "The rule names no frequency.";
                return false;
            }

            if (count is not null && until is not null)
            {
                error = "A rule ends either by count or by date, not both.";
                return false;
            }

            rule = new RecurrenceRule
            {
                Frequency = frequency.Value,
                Interval = interval,
                Count = count,
                Until = until,
                ByDay = byDay.Distinct().ToList(),
                ByMonthDay = byMonthDay.Distinct().ToList(),
                ByMonth = byMonth.Distinct().OrderBy(x => x).ToList()
            };

            return true;
        }

        /// <summary>
        /// Parses a rule, answering <see langword="null"/> for one that is empty or not valid.
        /// </summary>
        /// <param name="text">The rule.</param>
        /// <returns>The rule, or <see langword="null"/>.</returns>
        public static RecurrenceRule Parse(string text)
        {
            return TryParse(text, out var rule, out _) ? rule : null;
        }

        /// <summary>
        /// Writes the rule in the notation of RFC 5545, without the <c>RRULE:</c> prefix.
        /// </summary>
        /// <returns>The rule.</returns>
        public override string ToString()
        {
            var builder = new StringBuilder("FREQ=").Append(Frequency.ToString().ToUpperInvariant());

            if (Interval > 1)
            {
                builder.Append(";INTERVAL=").Append(Interval.ToString(CultureInfo.InvariantCulture));
            }

            if (ByMonth.Count > 0)
            {
                builder.Append(";BYMONTH=").Append(string.Join(",", ByMonth.Select(x => x.ToString(CultureInfo.InvariantCulture))));
            }

            if (ByMonthDay.Count > 0)
            {
                builder.Append(";BYMONTHDAY=").Append(string.Join(",", ByMonthDay.Select(x => x.ToString(CultureInfo.InvariantCulture))));
            }

            if (ByDay.Count > 0)
            {
                builder.Append(";BYDAY=").Append(string.Join(",", ByDay.Select(FormatWeekday)));
            }

            if (Count is not null)
            {
                builder.Append(";COUNT=").Append(Count.Value.ToString(CultureInfo.InvariantCulture));
            }
            else if (Until is not null)
            {
                builder.Append(";UNTIL=").Append(Until.Value.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        /// <summary>
        /// Answers the local starts of the occurrences of a series, in ascending order.
        /// </summary>
        /// <param name="start">The local start of the first occurrence.</param>
        /// <param name="toUtc">Converts a local start to UTC, for comparing it with
        /// <see cref="Until"/>; the identity when <see langword="null"/>.</param>
        /// <param name="windowEnd">A local start from which on no occurrence is wanted, or
        /// <see langword="null"/> for the whole series.</param>
        /// <returns>The local starts.</returns>
        public IEnumerable<DateTime> Expand(DateTime start, Func<DateTime, DateTime> toUtc = null, DateTime? windowEnd = null)
        {
            toUtc ??= x => x;

            var produced = 0;
            var limit = Math.Min(Count ?? MaxOccurrences, MaxOccurrences);
            var time = start.TimeOfDay;

            // the walk over the periods ends at the latest after this many periods, so that a
            // rule that never matches (the 30th of February) cannot spin
            const int maxPeriods = 100_000;

            // the start of the appointment is its first occurrence, whether the rule names its
            // day or not
            if ((Until is not null && toUtc(start) > Until.Value) || (windowEnd is not null && start >= windowEnd.Value))
            {
                yield break;
            }

            produced++;
            yield return start;

            if (produced >= limit)
            {
                yield break;
            }

            for (var period = 0; period < maxPeriods; period++)
            {
                var candidates = Candidates(start, period, time);

                if (candidates is null)
                {
                    yield break;
                }

                foreach (var candidate in candidates)
                {
                    if (candidate <= start)
                    {
                        continue;
                    }

                    if (windowEnd is not null && candidate >= windowEnd.Value)
                    {
                        yield break;
                    }

                    if (Until is not null && toUtc(candidate) > Until.Value)
                    {
                        yield break;
                    }

                    produced++;
                    yield return candidate;

                    if (produced >= limit)
                    {
                        yield break;
                    }
                }
            }
        }

        /// <summary>
        /// Answers the candidate starts of one period of the series - one day, week, month or
        /// year - in ascending order.
        /// </summary>
        /// <param name="start">The local start of the series.</param>
        /// <param name="period">The number of the period, counting from the first.</param>
        /// <param name="time">The time of day of every occurrence.</param>
        /// <returns>The candidates, or <see langword="null"/> past the representable dates.</returns>
        private List<DateTime> Candidates(DateTime start, int period, TimeSpan time)
        {
            try
            {
                switch (Frequency)
                {
                    case RecurrenceFrequency.Daily:
                        {
                            var day = start.Date.AddDays((long)period * Interval);

                            return Matches(day) ? [day + time] : [];
                        }

                    case RecurrenceFrequency.Weekly:
                        {
                            var weekStart = StartOfWeek(start.Date).AddDays(7L * period * Interval);
                            var days = ByDay.Count > 0
                                ? ByDay.Select(x => x.Day).Distinct()
                                : [start.DayOfWeek];

                            return [.. days
                                .Select(x => weekStart.AddDays(((int)x + 6) % 7))
                                .Where(x => ByMonth.Count == 0 || ByMonth.Contains(x.Month))
                                .OrderBy(x => x)
                                .Select(x => x + time)];
                        }

                    case RecurrenceFrequency.Monthly:
                        {
                            var month = new DateTime(start.Year, start.Month, 1).AddMonths(period * Interval);

                            if (ByMonth.Count > 0 && !ByMonth.Contains(month.Month))
                            {
                                return [];
                            }

                            return [.. DaysOfMonth(month, start).Select(x => x + time)];
                        }

                    case RecurrenceFrequency.Yearly:
                        {
                            var year = start.Year + period * Interval;

                            if (year > 9998)
                            {
                                return null;
                            }

                            var months = ByMonth.Count > 0 ? ByMonth : [start.Month];
                            var result = new List<DateTime>();

                            foreach (var month in months)
                            {
                                var first = new DateTime(year, month, 1);

                                // a yearly rule naming only weekdays without a month means those
                                // weekdays in the month of the start, which is what calendars write
                                result.AddRange(DaysOfMonth(first, start));
                            }

                            return [.. result.OrderBy(x => x).Select(x => x + time)];
                        }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }

            return null;
        }

        /// <summary>
        /// Answers the days of one month a monthly (or yearly) rule falls on.
        /// </summary>
        /// <param name="month">The first day of the month.</param>
        /// <param name="start">The local start of the series.</param>
        /// <returns>The days, in ascending order.</returns>
        private IEnumerable<DateTime> DaysOfMonth(DateTime month, DateTime start)
        {
            var length = DateTime.DaysInMonth(month.Year, month.Month);
            var days = new SortedSet<DateTime>();

            if (ByMonthDay.Count > 0)
            {
                foreach (var day in ByMonthDay)
                {
                    var number = day > 0 ? day : length + day + 1;

                    if (number >= 1 && number <= length)
                    {
                        var date = new DateTime(month.Year, month.Month, number);

                        // with both, a day has to be one of the weekdays as well
                        if (ByDay.Count == 0 || ByDay.Any(x => x.Day == date.DayOfWeek))
                        {
                            days.Add(date);
                        }
                    }
                }

                return days;
            }

            if (ByDay.Count > 0)
            {
                foreach (var weekday in ByDay)
                {
                    var matching = Enumerable.Range(1, length)
                        .Select(x => new DateTime(month.Year, month.Month, x))
                        .Where(x => x.DayOfWeek == weekday.Day)
                        .ToList();

                    if (weekday.Ordinal == 0)
                    {
                        days.UnionWith(matching);
                    }
                    else
                    {
                        var index = weekday.Ordinal > 0 ? weekday.Ordinal - 1 : matching.Count + weekday.Ordinal;

                        if (index >= 0 && index < matching.Count)
                        {
                            days.Add(matching[index]);
                        }
                    }
                }

                return days;
            }

            // the day of the month of the start; a month that has no such day has no occurrence
            if (start.Day <= length)
            {
                days.Add(new DateTime(month.Year, month.Month, start.Day));
            }

            return days;
        }

        /// <summary>
        /// Determines whether a day matches the filters of a daily rule.
        /// </summary>
        /// <param name="day">The day.</param>
        /// <returns><see langword="true"/> when the day is one the rule falls on.</returns>
        private bool Matches(DateTime day)
        {
            if (ByMonth.Count > 0 && !ByMonth.Contains(day.Month))
            {
                return false;
            }

            if (ByDay.Count > 0 && !ByDay.Any(x => x.Day == day.DayOfWeek))
            {
                return false;
            }

            if (ByMonthDay.Count > 0)
            {
                var length = DateTime.DaysInMonth(day.Year, day.Month);

                return ByMonthDay.Any(x => (x > 0 ? x : length + x + 1) == day.Day);
            }

            return true;
        }

        /// <summary>
        /// Answers the Monday of the week a day is in.
        /// </summary>
        /// <param name="day">The day.</param>
        /// <returns>The Monday.</returns>
        private static DateTime StartOfWeek(DateTime day)
        {
            return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        }

        /// <summary>
        /// Reads a weekday token (<c>MO</c>, <c>2TU</c>, <c>-1FR</c>).
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="weekday">The weekday read.</param>
        /// <returns><see langword="true"/> when the token is a weekday.</returns>
        private static bool TryParseWeekday(string token, out RecurrenceWeekday weekday)
        {
            weekday = default;

            if (token is null || token.Length < 2)
            {
                return false;
            }

            var code = token[^2..].ToUpperInvariant();
            var match = _days.FirstOrDefault(x => x.Token == code);

            if (match.Token is null)
            {
                return false;
            }

            var ordinal = 0;
            var prefix = token[..^2];

            if (prefix.Length > 0 && (!int.TryParse(prefix, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out ordinal) || ordinal == 0 || ordinal < -53 || ordinal > 53))
            {
                return false;
            }

            weekday = new RecurrenceWeekday(match.Day, ordinal);

            return true;
        }

        /// <summary>
        /// Writes a weekday token.
        /// </summary>
        /// <param name="weekday">The weekday.</param>
        /// <returns>The token.</returns>
        private static string FormatWeekday(RecurrenceWeekday weekday)
        {
            var code = _days.First(x => x.Day == weekday.Day).Token;

            return weekday.Ordinal == 0 ? code : weekday.Ordinal.ToString(CultureInfo.InvariantCulture) + code;
        }

        /// <summary>
        /// Answers the token of a weekday (<c>MO</c> for Monday).
        /// </summary>
        /// <param name="day">The weekday.</param>
        /// <returns>The token.</returns>
        public static string Token(DayOfWeek day)
        {
            return _days.First(x => x.Day == day).Token;
        }

        /// <summary>
        /// Reads the end date of a rule: a date (<c>20261231</c>, the end of that day) or a UTC
        /// moment (<c>20261231T235959Z</c>); a floating moment is taken as UTC.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The moment in UTC, or <see langword="null"/>.</returns>
        private static DateTime? ParseUntil(string value)
        {
            var text = value.Trim().TrimEnd('Z', 'z');

            if (DateTime.TryParseExact(text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment))
            {
                return DateTime.SpecifyKind(moment, DateTimeKind.Utc);
            }

            if (DateTime.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return DateTime.SpecifyKind(date.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            }

            return null;
        }
    }
}
