using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// An attendee of an event of an iCalendar file.
    /// </summary>
    public sealed class ICalendarAttendee
    {
        /// <summary>
        /// Gets or sets the display name (<c>CN</c>).
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the mail address (the <c>mailto:</c> value).
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Gets or sets the participation role (<c>ROLE</c>), for instance <c>REQ-PARTICIPANT</c>.
        /// </summary>
        public string Role { get; set; }

        /// <summary>
        /// Gets or sets the participation status (<c>PARTSTAT</c>), for instance <c>ACCEPTED</c>.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets a comment the attendee gave, or <see langword="null"/>. iCalendar has no
        /// parameter for it; it travels in the non-standard <c>X-COMMENT</c> parameter and is
        /// ignored by calendars that do not know it.
        /// </summary>
        public string Comment { get; set; }
    }

    /// <summary>
    /// An event (<c>VEVENT</c>) of an iCalendar file.
    /// </summary>
    public sealed class ICalendarEvent
    {
        /// <summary>
        /// Gets or sets the unique identifier (<c>UID</c>).
        /// </summary>
        public string Uid { get; set; }

        /// <summary>
        /// Gets or sets the title (<c>SUMMARY</c>).
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the description as plain text (<c>DESCRIPTION</c>).
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the place (<c>LOCATION</c>).
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the start in UTC (<c>DTSTART</c>).
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Gets or sets the end in UTC (<c>DTEND</c>, or the start plus <c>DURATION</c>).
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Gets or sets whether the event covers whole days (a <c>DATE</c> value).
        /// </summary>
        public bool AllDay { get; set; }

        /// <summary>
        /// Gets or sets the time zone the event was written in (<c>TZID</c>), or <see langword="null"/>.
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Gets or sets the recurrence rule (<c>RRULE</c>), or <see langword="null"/>.
        /// </summary>
        public string Recurrence { get; set; }

        /// <summary>
        /// Gets the starts of the occurrences removed from the series (<c>EXDATE</c>), in UTC.
        /// </summary>
        public List<DateTime> Exceptions { get; } = [];

        /// <summary>
        /// Gets or sets the occurrence of a series this event replaces (<c>RECURRENCE-ID</c>),
        /// in UTC, or <see langword="null"/>.
        /// </summary>
        public DateTime? RecurrenceId { get; set; }

        /// <summary>
        /// Gets or sets the status (<c>STATUS</c>): <c>TENTATIVE</c>, <c>CONFIRMED</c> or
        /// <c>CANCELLED</c>.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets the organizer.
        /// </summary>
        public ICalendarAttendee Organizer { get; set; }

        /// <summary>
        /// Gets the attendees.
        /// </summary>
        public List<ICalendarAttendee> Attendees { get; } = [];

        /// <summary>
        /// Gets or sets the address of the event (<c>URL</c>).
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets the categories (<c>CATEGORIES</c>).
        /// </summary>
        public List<string> Categories { get; } = [];

        /// <summary>
        /// Gets or sets the time of the last change (<c>LAST-MODIFIED</c>), in UTC.
        /// </summary>
        public DateTime? Modified { get; set; }
    }

    /// <summary>
    /// Reads and writes calendar files in the format of RFC 5545 (iCalendar, <c>.ics</c>):
    /// the events with their times, series, exceptions, attendees and answers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The writer writes every time in UTC (or as a date for an all-day event) and names the
    /// zone a series was planned in, so that a calendar repeats it in that zone; it folds lines
    /// at 75 octets and escapes text as the RFC says. Every calendar the format is meant for -
    /// Outlook, Google, Apple, Thunderbird - reads that.
    /// </para>
    /// <para>
    /// The reader is lenient in what it accepts: folded lines, both line endings, times in UTC,
    /// in a named zone (a <c>TZID</c> is resolved as an IANA or Windows id, the definitions of
    /// <c>VTIMEZONE</c> are not needed for that) or floating (taken in the zone the caller
    /// names), <c>DTEND</c> or <c>DURATION</c>. What it cannot read of an event it skips rather
    /// than refuse the file.
    /// </para>
    /// </remarks>
    public static class ICalendar
    {
        /// <summary>
        /// The media type of an iCalendar file.
        /// </summary>
        public const string ContentType = "text/calendar";

        /// <summary>
        /// Writes events as a calendar file.
        /// </summary>
        /// <param name="events">The events.</param>
        /// <param name="name">The name of the calendar (<c>X-WR-CALNAME</c>), or <see langword="null"/>.</param>
        /// <returns>The file content.</returns>
        public static string Write(IEnumerable<ICalendarEvent> events, string name = null)
        {
            var builder = new StringBuilder();

            Line(builder, "BEGIN:VCALENDAR");
            Line(builder, "VERSION:2.0");
            Line(builder, "PRODID:-//KleeneStar//Appointment//EN");
            Line(builder, "CALSCALE:GREGORIAN");
            Line(builder, "METHOD:PUBLISH");

            if (!string.IsNullOrWhiteSpace(name))
            {
                Line(builder, "X-WR-CALNAME:" + Escape(name));
            }

            var stamp = FormatUtc(DateTime.UtcNow);

            foreach (var item in events ?? [])
            {
                if (item is null)
                {
                    continue;
                }

                Line(builder, "BEGIN:VEVENT");
                Line(builder, "UID:" + Escape(item.Uid ?? Guid.NewGuid().ToString()));
                Line(builder, "DTSTAMP:" + stamp);

                if (item.AllDay)
                {
                    var zone = AppointmentTime.Zone(item.TimeZone);
                    var firstDay = AppointmentTime.ToLocal(item.Start, zone).Date;
                    var afterLast = AppointmentTime.ToLocal(item.End, zone).Date;

                    if (afterLast <= firstDay)
                    {
                        afterLast = firstDay.AddDays(1);
                    }

                    Line(builder, "DTSTART;VALUE=DATE:" + firstDay.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                    Line(builder, "DTEND;VALUE=DATE:" + afterLast.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                }
                else if (!string.IsNullOrWhiteSpace(item.Recurrence) && !string.IsNullOrWhiteSpace(item.TimeZone))
                {
                    // a series is repeated in the zone it was planned in, so it is written there
                    var zone = AppointmentTime.Zone(item.TimeZone);
                    var tzid = IanaId(zone);

                    Line(builder, $"DTSTART;TZID={tzid}:" + FormatLocal(AppointmentTime.ToLocal(item.Start, zone)));
                    Line(builder, $"DTEND;TZID={tzid}:" + FormatLocal(AppointmentTime.ToLocal(item.End, zone)));
                }
                else
                {
                    Line(builder, "DTSTART:" + FormatUtc(item.Start));
                    Line(builder, "DTEND:" + FormatUtc(item.End));
                }

                if (!string.IsNullOrWhiteSpace(item.Recurrence))
                {
                    Line(builder, "RRULE:" + item.Recurrence.Trim());

                    foreach (var exception in item.Exceptions.Distinct().OrderBy(x => x))
                    {
                        if (item.AllDay)
                        {
                            var zone = AppointmentTime.Zone(item.TimeZone);
                            Line(builder, "EXDATE;VALUE=DATE:" + AppointmentTime.ToLocal(exception, zone).ToString("yyyyMMdd", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            Line(builder, "EXDATE:" + FormatUtc(exception));
                        }
                    }
                }

                if (item.RecurrenceId is not null)
                {
                    Line(builder, "RECURRENCE-ID:" + FormatUtc(item.RecurrenceId.Value));
                }

                Line(builder, "SUMMARY:" + Escape(item.Summary ?? string.Empty));

                if (!string.IsNullOrWhiteSpace(item.Description))
                {
                    Line(builder, "DESCRIPTION:" + Escape(item.Description));
                }

                if (!string.IsNullOrWhiteSpace(item.Location))
                {
                    Line(builder, "LOCATION:" + Escape(item.Location));
                }

                if (!string.IsNullOrWhiteSpace(item.Status))
                {
                    Line(builder, "STATUS:" + item.Status.Trim().ToUpperInvariant());
                }

                if (item.Categories.Count > 0)
                {
                    Line(builder, "CATEGORIES:" + string.Join(",", item.Categories.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Escape)));
                }

                if (!string.IsNullOrWhiteSpace(item.Url))
                {
                    Line(builder, "URL:" + item.Url.Trim());
                }

                if (item.Modified is not null)
                {
                    Line(builder, "LAST-MODIFIED:" + FormatUtc(item.Modified.Value));
                }

                if (item.Organizer is not null)
                {
                    Line(builder, "ORGANIZER" + Parameters(item.Organizer, false) + ":" + Address(item.Organizer));
                }

                foreach (var attendee in item.Attendees)
                {
                    Line(builder, "ATTENDEE" + Parameters(attendee, true) + ":" + Address(attendee));
                }

                Line(builder, "END:VEVENT");
            }

            Line(builder, "END:VCALENDAR");

            return builder.ToString();
        }

        /// <summary>
        /// Reads the events of a calendar file.
        /// </summary>
        /// <param name="text">The file content.</param>
        /// <param name="defaultZone">The zone floating times are taken in, or <see langword="null"/>
        /// for the host's.</param>
        /// <returns>The events; an event without a readable start is skipped.</returns>
        public static IReadOnlyList<ICalendarEvent> Read(string text, string defaultZone = null)
        {
            var events = new List<ICalendarEvent>();
            ICalendarEvent current = null;
            var depth = 0;
            TimeSpan? duration = null;
            var hasEnd = false;

            foreach (var line in Unfold(text))
            {
                if (!TryParseLine(line, out var name, out var parameters, out var value))
                {
                    continue;
                }

                if (name == "BEGIN")
                {
                    if (string.Equals(value, "VEVENT", StringComparison.OrdinalIgnoreCase) && current is null)
                    {
                        current = new ICalendarEvent();
                        duration = null;
                        hasEnd = false;
                        depth = 0;
                    }
                    else if (current is not null)
                    {
                        // a component inside the event (an alarm) is skipped with its properties
                        depth++;
                    }

                    continue;
                }

                if (name == "END")
                {
                    if (current is not null && depth > 0)
                    {
                        depth--;
                    }
                    else if (current is not null && string.Equals(value, "VEVENT", StringComparison.OrdinalIgnoreCase))
                    {
                        if (current.Start != default)
                        {
                            if (!hasEnd)
                            {
                                current.End = duration is not null
                                    ? current.Start + duration.Value
                                    : current.AllDay ? current.Start.AddDays(1) : current.Start;
                            }

                            if (current.End < current.Start)
                            {
                                current.End = current.Start;
                            }

                            events.Add(current);
                        }

                        current = null;
                    }

                    continue;
                }

                if (current is null || depth > 0)
                {
                    continue;
                }

                switch (name)
                {
                    case "UID":
                        current.Uid = value.Trim();
                        break;

                    case "SUMMARY":
                        current.Summary = Unescape(value);
                        break;

                    case "DESCRIPTION":
                        current.Description = Unescape(value);
                        break;

                    case "LOCATION":
                        current.Location = Unescape(value);
                        break;

                    case "STATUS":
                        current.Status = value.Trim().ToUpperInvariant();
                        break;

                    case "URL":
                        current.Url = value.Trim();
                        break;

                    case "RRULE":
                        current.Recurrence = value.Trim();
                        break;

                    case "CATEGORIES":
                        current.Categories.AddRange(SplitList(value).Select(Unescape).Where(x => !string.IsNullOrWhiteSpace(x)));
                        break;

                    case "DTSTART":
                        {
                            if (TryParseTime(value, parameters, defaultZone, out var start, out var allDay, out var zone))
                            {
                                current.Start = start;
                                current.AllDay = allDay;
                                current.TimeZone = zone;
                            }

                            break;
                        }

                    case "DTEND":
                        {
                            if (TryParseTime(value, parameters, defaultZone, out var end, out _, out _))
                            {
                                current.End = end;
                                hasEnd = true;
                            }

                            break;
                        }

                    case "DURATION":
                        duration = ParseDuration(value);
                        break;

                    case "EXDATE":
                        foreach (var part in SplitList(value))
                        {
                            if (TryParseTime(part, parameters, defaultZone, out var exception, out _, out _))
                            {
                                current.Exceptions.Add(exception);
                            }
                        }

                        break;

                    case "RECURRENCE-ID":
                        {
                            if (TryParseTime(value, parameters, defaultZone, out var recurrenceId, out _, out _))
                            {
                                current.RecurrenceId = recurrenceId;
                            }

                            break;
                        }

                    case "LAST-MODIFIED":
                        {
                            if (TryParseTime(value, parameters, defaultZone, out var modified, out _, out _))
                            {
                                current.Modified = modified;
                            }

                            break;
                        }

                    case "ORGANIZER":
                        current.Organizer = ReadAttendee(value, parameters);
                        break;

                    case "ATTENDEE":
                        current.Attendees.Add(ReadAttendee(value, parameters));
                        break;
                }
            }

            // an all-day event read without zone is placed in the zone of the reader
            foreach (var item in events.Where(x => x.AllDay && x.TimeZone is null))
            {
                item.TimeZone = defaultZone;
            }

            return events;
        }

        /// <summary>
        /// Writes a content line, folded at 75 octets.
        /// </summary>
        /// <param name="builder">The file being written.</param>
        /// <param name="line">The line.</param>
        private static void Line(StringBuilder builder, string line)
        {
            var bytes = 0;
            var first = true;

            foreach (var rune in line.EnumerateRunes())
            {
                var size = rune.Utf8SequenceLength;

                // the first line holds 75 octets, a continuation line 74 after its space
                if (bytes + size > (first ? 75 : 74))
                {
                    builder.Append("\r\n ");
                    bytes = 0;
                    first = false;
                }

                builder.Append(rune.ToString());
                bytes += size;
            }

            builder.Append("\r\n");
        }

        /// <summary>
        /// Joins folded lines.
        /// </summary>
        /// <param name="text">The file content.</param>
        /// <returns>The content lines.</returns>
        private static IEnumerable<string> Unfold(string text)
        {
            var lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var current = new StringBuilder();

            foreach (var line in lines)
            {
                if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                {
                    current.Append(line, 1, line.Length - 1);
                    continue;
                }

                if (current.Length > 0)
                {
                    yield return current.ToString();
                }

                current.Clear().Append(line);
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        /// <summary>
        /// Splits a content line into its name, parameters and value.
        /// </summary>
        /// <param name="line">The line.</param>
        /// <param name="name">The property name, upper case.</param>
        /// <param name="parameters">The parameters, names upper case.</param>
        /// <param name="value">The value.</param>
        /// <returns><see langword="true"/> for a line that has a name and a value.</returns>
        private static bool TryParseLine(string line, out string name, out Dictionary<string, string> parameters, out string value)
        {
            name = null;
            value = null;
            parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            // the value starts at the first colon outside a quoted parameter value
            var quoted = false;
            var colon = -1;

            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                {
                    quoted = !quoted;
                }
                else if (line[i] == ':' && !quoted)
                {
                    colon = i;
                    break;
                }
            }

            if (colon <= 0)
            {
                return false;
            }

            var head = line[..colon];
            value = line[(colon + 1)..];

            var parts = SplitOutsideQuotes(head, ';');
            name = parts[0].Trim().ToUpperInvariant();

            foreach (var part in parts.Skip(1))
            {
                var equals = part.IndexOf('=');

                if (equals > 0)
                {
                    parameters[part[..equals].Trim()] = part[(equals + 1)..].Trim().Trim('"');
                }
            }

            return name.Length > 0;
        }

        /// <summary>
        /// Splits a text at a separator that is not inside double quotes.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="separator">The separator.</param>
        /// <returns>The parts.</returns>
        private static List<string> SplitOutsideQuotes(string text, char separator)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            var quoted = false;

            foreach (var c in text)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }

                if (c == separator && !quoted)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            parts.Add(current.ToString());

            return parts;
        }

        /// <summary>
        /// Splits a list value at commas that are not escaped.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The items, still escaped.</returns>
        private static IEnumerable<string> SplitList(string value)
        {
            var current = new StringBuilder();

            for (var i = 0; i < (value ?? string.Empty).Length; i++)
            {
                if (value[i] == '\\' && i + 1 < value.Length)
                {
                    current.Append(value[i]).Append(value[i + 1]);
                    i++;
                    continue;
                }

                if (value[i] == ',')
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                    continue;
                }

                current.Append(value[i]);
            }

            if (current.Length > 0)
            {
                yield return current.ToString().Trim();
            }
        }

        /// <summary>
        /// Reads a date or a date with time.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="parameters">The parameters of the property (<c>TZID</c>, <c>VALUE</c>).</param>
        /// <param name="defaultZone">The zone of a floating time.</param>
        /// <param name="utc">The instant in UTC (a date is its midnight in the zone).</param>
        /// <param name="allDay">Whether the value is a date.</param>
        /// <param name="zone">The zone named by the property, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the value was read.</returns>
        private static bool TryParseTime(string value, Dictionary<string, string> parameters, string defaultZone, out DateTime utc, out bool allDay, out string zone)
        {
            utc = default;
            allDay = false;
            parameters.TryGetValue("TZID", out zone);

            var text = (value ?? string.Empty).Trim();
            var isDate = parameters.TryGetValue("VALUE", out var kind) && string.Equals(kind, "DATE", StringComparison.OrdinalIgnoreCase)
                || (text.Length == 8 && text.All(char.IsDigit));

            if (isDate)
            {
                if (!DateTime.TryParseExact(text[..Math.Min(8, text.Length)], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return false;
                }

                allDay = true;
                utc = AppointmentTime.ToUtc(date, AppointmentTime.Zone(zone ?? defaultZone));

                return true;
            }

            if (text.EndsWith('Z') || text.EndsWith('z'))
            {
                if (!DateTime.TryParseExact(text[..^1], "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var moment))
                {
                    return false;
                }

                utc = DateTime.SpecifyKind(moment, DateTimeKind.Utc);
                zone ??= null;

                return true;
            }

            if (!DateTime.TryParseExact(text, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)
                && !DateTime.TryParseExact(text, "yyyyMMdd'T'HHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                return false;
            }

            utc = AppointmentTime.ToUtc(local, AppointmentTime.Zone(zone ?? defaultZone));

            return true;
        }

        /// <summary>
        /// Reads a duration (<c>PT1H30M</c>, <c>P1D</c>, <c>P1W</c>).
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The duration, or <see langword="null"/>.</returns>
        private static TimeSpan? ParseDuration(string value)
        {
            var text = (value ?? string.Empty).Trim().ToUpperInvariant();
            var negative = text.StartsWith('-');
            text = text.TrimStart('+', '-');

            if (!text.StartsWith('P'))
            {
                return null;
            }

            var result = TimeSpan.Zero;
            var number = new StringBuilder();
            var inTime = false;

            foreach (var c in text[1..])
            {
                if (char.IsDigit(c))
                {
                    number.Append(c);
                    continue;
                }

                if (c == 'T')
                {
                    inTime = true;
                    continue;
                }

                if (!int.TryParse(number.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount))
                {
                    return null;
                }

                number.Clear();

                result += c switch
                {
                    'W' => TimeSpan.FromDays(7 * amount),
                    'D' => TimeSpan.FromDays(amount),
                    'H' when inTime => TimeSpan.FromHours(amount),
                    'M' when inTime => TimeSpan.FromMinutes(amount),
                    'S' when inTime => TimeSpan.FromSeconds(amount),
                    _ => TimeSpan.Zero
                };
            }

            return negative ? -result : result;
        }

        /// <summary>
        /// Reads an attendee or organizer.
        /// </summary>
        /// <param name="value">The value (a <c>mailto:</c> address).</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>The attendee.</returns>
        private static ICalendarAttendee ReadAttendee(string value, Dictionary<string, string> parameters)
        {
            var address = (value ?? string.Empty).Trim();

            if (address.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                address = address[7..];
            }

            parameters.TryGetValue("CN", out var name);
            parameters.TryGetValue("ROLE", out var role);
            parameters.TryGetValue("PARTSTAT", out var status);
            parameters.TryGetValue("X-COMMENT", out var comment);

            return new ICalendarAttendee
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Email = string.IsNullOrWhiteSpace(address) ? null : address,
                Role = role?.ToUpperInvariant(),
                Status = status?.ToUpperInvariant(),
                Comment = string.IsNullOrWhiteSpace(comment) ? null : comment
            };
        }

        /// <summary>
        /// Writes the parameters of an attendee or organizer.
        /// </summary>
        /// <param name="attendee">The attendee.</param>
        /// <param name="participation">Whether role and status are written.</param>
        /// <returns>The parameters, each with its leading semicolon.</returns>
        private static string Parameters(ICalendarAttendee attendee, bool participation)
        {
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(attendee.Name))
            {
                builder.Append(";CN=").Append(Quote(attendee.Name));
            }

            if (participation)
            {
                if (!string.IsNullOrWhiteSpace(attendee.Role))
                {
                    builder.Append(";ROLE=").Append(attendee.Role);
                }

                if (!string.IsNullOrWhiteSpace(attendee.Status))
                {
                    builder.Append(";PARTSTAT=").Append(attendee.Status);
                }

                if (!string.IsNullOrWhiteSpace(attendee.Comment))
                {
                    builder.Append(";X-COMMENT=").Append(Quote(attendee.Comment));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Writes the address of an attendee: the mail address, or an address naming the guest
        /// for one without.
        /// </summary>
        /// <param name="attendee">The attendee.</param>
        /// <returns>The value.</returns>
        private static string Address(ICalendarAttendee attendee)
        {
            return string.IsNullOrWhiteSpace(attendee.Email)
                ? "urn:kleenestar:guest:" + Uri.EscapeDataString(attendee.Name ?? "guest")
                : "mailto:" + attendee.Email.Trim();
        }

        /// <summary>
        /// Quotes a parameter value; a double quote cannot be written in one and is dropped.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The quoted value.</returns>
        private static string Quote(string value)
        {
            var clean = (value ?? string.Empty).Replace("\"", string.Empty).Replace("\r", " ").Replace("\n", " ");

            return "\"" + clean + "\"";
        }

        /// <summary>
        /// Escapes a text value.
        /// </summary>
        /// <param name="value">The text.</param>
        /// <returns>The escaped text.</returns>
        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace(";", "\\;")
                .Replace(",", "\\,")
                .Replace("\r\n", "\\n")
                .Replace("\n", "\\n")
                .Replace("\r", "\\n");
        }

        /// <summary>
        /// Unescapes a text value.
        /// </summary>
        /// <param name="value">The escaped text.</param>
        /// <returns>The text.</returns>
        private static string Unescape(string value)
        {
            var builder = new StringBuilder();
            var text = value ?? string.Empty;

            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    var next = text[i + 1];
                    builder.Append(next is 'n' or 'N' ? '\n' : next);
                    i++;
                    continue;
                }

                builder.Append(text[i]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Writes an instant in UTC.
        /// </summary>
        /// <param name="utc">The instant.</param>
        /// <returns>The value.</returns>
        private static string FormatUtc(DateTime utc)
        {
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Writes a local time.
        /// </summary>
        /// <param name="local">The local time.</param>
        /// <returns>The value.</returns>
        private static string FormatLocal(DateTime local)
        {
            return local.ToString("yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Answers the IANA id of a zone, which every calendar reads; the .NET id where there
        /// is none.
        /// </summary>
        /// <param name="zone">The zone.</param>
        /// <returns>The id.</returns>
        private static string IanaId(TimeZoneInfo zone)
        {
            if (zone.HasIanaId)
            {
                return zone.Id;
            }

            return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
        }
    }
}
