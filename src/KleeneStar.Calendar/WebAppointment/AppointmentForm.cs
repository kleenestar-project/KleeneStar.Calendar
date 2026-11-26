using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// What a submitted appointment dialog asks for.
    /// </summary>
    public sealed class AppointmentDraft
    {
        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Gets or sets the description (an editor document), or <see langword="null"/> when the
        /// dialog did not carry one.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the start of the (first) occurrence in UTC.
        /// </summary>
        public DateTime Start { get; set; }

        /// <summary>
        /// Gets or sets the end of the (first) occurrence in UTC.
        /// </summary>
        public DateTime End { get; set; }

        /// <summary>
        /// Gets or sets whether the appointment covers whole days.
        /// </summary>
        public bool AllDay { get; set; }

        /// <summary>
        /// Gets or sets the zone the times were entered in.
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Gets or sets the place.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Gets or sets the recurrence rule, or <see langword="null"/>.
        /// </summary>
        public string Recurrence { get; set; }

        /// <summary>
        /// Gets the accounts invited.
        /// </summary>
        public List<Guid> IdentityIds { get; } = [];

        /// <summary>
        /// Gets the guests invited.
        /// </summary>
        public List<AppointmentParticipant> Guests { get; } = [];

        /// <summary>
        /// Gets the resources booked.
        /// </summary>
        public List<Guid> ResourceIds { get; } = [];

        /// <summary>
        /// Gets or sets whether the appointment is closed when its time is over.
        /// </summary>
        public bool AutoClose { get; set; } = true;

        /// <summary>
        /// Gets or sets whether a resource booked twice is booked anyway.
        /// </summary>
        public bool IgnoreConflicts { get; set; }
    }

    /// <summary>
    /// The appointment dialog: the names of its inputs, the values it starts from and the reading
    /// of what it submits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The times are entered as a date (the date input, in the short date pattern of the culture)
    /// and a time of day (<c>HH:mm</c>) in the zone the dialog names, which starts as the zone the
    /// appointment was planned in, or the reader's for a new one.
    /// </para>
    /// <para>
    /// A series is entered as a frequency, an interval, the weekdays of a weekly series and an
    /// end by date or count; a rule those cannot express (the second Tuesday of every month, say)
    /// is shown and kept as written in the rule input, which wins whenever it is filled.
    /// </para>
    /// </remarks>
    public static class AppointmentForm
    {
        /// <summary>Input name of the title.</summary>
        public const string Summary = "Summary";

        /// <summary>Input name of the description.</summary>
        public const string Description = "Description";

        /// <summary>Input name of the class (new appointments only).</summary>
        public const string ClassId = "ClassId";

        /// <summary>Input name of the template (new appointments only).</summary>
        public const string TemplateId = "TemplateId";

        /// <summary>Input name of the first day.</summary>
        public const string StartDate = "StartDate";

        /// <summary>Input name of the start time.</summary>
        public const string StartTime = "StartTime";

        /// <summary>Input name of the last day.</summary>
        public const string EndDate = "EndDate";

        /// <summary>Input name of the end time.</summary>
        public const string EndTime = "EndTime";

        /// <summary>Input name of the all-day switch.</summary>
        public const string AllDay = "AllDay";

        /// <summary>Input name of the zone.</summary>
        public const string TimeZone = "TimeZone";

        /// <summary>Input name of the place.</summary>
        public const string Location = "Location";

        /// <summary>Input name of the frequency of a series.</summary>
        public const string Repeat = "Repeat";

        /// <summary>Input name of the interval of a series.</summary>
        public const string Interval = "Interval";

        /// <summary>Input name of the weekdays of a weekly series.</summary>
        public const string Weekdays = "Weekdays";

        /// <summary>Input name of the last day of a series.</summary>
        public const string Until = "Until";

        /// <summary>Input name of the number of occurrences of a series.</summary>
        public const string Count = "Count";

        /// <summary>Input name of a rule written out.</summary>
        public const string Rule = "Rule";

        /// <summary>Input name of the accounts invited.</summary>
        public const string Participants = "Participants";

        /// <summary>Input name of the guests invited.</summary>
        public const string Guests = "Guests";

        /// <summary>Input name of the resources booked.</summary>
        public const string Resources = "Resources";

        /// <summary>Input name of the automatic closing.</summary>
        public const string AutoClose = "AutoClose";

        /// <summary>Input name of the booking despite conflicts.</summary>
        public const string IgnoreConflicts = "IgnoreConflicts";

        /// <summary>
        /// The frequencies the dialog offers, by the id of their option.
        /// </summary>
        public static IReadOnlyList<string> Frequencies { get; } = ["none", "daily", "weekly", "monthly", "yearly", "custom"];

        /// <summary>
        /// The weekdays the dialog offers, by the id of their option (the token of RFC 5545).
        /// </summary>
        public static IReadOnlyList<string> Days { get; } = ["MO", "TU", "WE", "TH", "FR", "SA", "SU"];

        /// <summary>
        /// The zones the dialog offers besides the zone of the host and of the appointment.
        /// </summary>
        public static IReadOnlyList<string> Zones { get; } =
        [
            "Europe/Berlin", "Europe/Vienna", "Europe/Zurich", "Europe/London", "Europe/Paris",
            "Europe/Madrid", "Europe/Rome", "Europe/Amsterdam", "Europe/Stockholm", "Europe/Warsaw",
            "Europe/Istanbul", "America/New_York", "America/Chicago", "America/Denver",
            "America/Los_Angeles", "America/Sao_Paulo", "Asia/Dubai", "Asia/Kolkata",
            "Asia/Singapore", "Asia/Shanghai", "Asia/Tokyo", "Australia/Sydney", "UTC"
        ];

        /// <summary>
        /// The pattern of a time of day.
        /// </summary>
        public const string TimePattern = "([01]?[0-9]|2[0-3])[:.][0-5][0-9]";

        /// <summary>
        /// Returns the values the dialog starts from.
        /// </summary>
        /// <param name="entity">The appointment, or <see langword="null"/> for a new one.</param>
        /// <param name="schedule">Its schedule, or <see langword="null"/>.</param>
        /// <param name="participants">Its participants.</param>
        /// <param name="resources">Its resources.</param>
        /// <param name="zone">The zone of a new appointment.</param>
        /// <param name="culture">The culture of the request.</param>
        /// <returns>The values, keyed by input name.</returns>
        public static Dictionary<string, object> Values(ObjectEntity entity, AppointmentEntity schedule, IEnumerable<AppointmentParticipant> participants, IEnumerable<Guid> resources, TimeZoneInfo zone, CultureInfo culture)
        {
            culture ??= CultureInfo.CurrentCulture;

            var planned = schedule is null ? zone : AppointmentTime.Zone(schedule.TimeZone);
            var start = schedule?.Start ?? NextHour(zone);
            var end = schedule?.End ?? start.AddHours(1);
            var allDay = schedule?.AllDay ?? false;
            var localStart = AppointmentTime.ToLocal(start, planned);
            var localEnd = AppointmentTime.ToLocal(end, planned);

            // a whole-day end is the midnight after the last day; the dialog names the last day
            var lastDay = allDay && localEnd > localStart ? localEnd.AddTicks(-1) : localEnd;

            var values = new Dictionary<string, object>
            {
                [Summary] = entity?.Summary ?? string.Empty,
                [Description] = entity?.Description ?? string.Empty,
                [StartDate] = localStart.ToString(culture.DateTimeFormat.ShortDatePattern, culture),
                [StartTime] = localStart.ToString("HH:mm", CultureInfo.InvariantCulture),
                [EndDate] = lastDay.ToString(culture.DateTimeFormat.ShortDatePattern, culture),
                [EndTime] = localEnd.ToString("HH:mm", CultureInfo.InvariantCulture),
                [AllDay] = allDay,
                [TimeZone] = ZoneOption(planned),
                [Location] = schedule?.Location ?? string.Empty,
                [AutoClose] = schedule?.AutoClose ?? true,
                [IgnoreConflicts] = false,
                [Participants] = (participants ?? []).Where(x => x.IdentityId is not null).Select(x => x.IdentityId.Value.ToString()).ToList(),
                [Guests] = string.Join("; ", (participants ?? []).Where(x => x.IdentityId is null).Select(FormatGuest)),
                [Resources] = (resources ?? []).Select(x => x.ToString()).ToList()
            };

            foreach (var (key, value) in Recurrence(schedule?.Recurrence, culture, planned))
            {
                values[key] = value;
            }

            return values;
        }

        /// <summary>
        /// Lays the calendar presets of a template over the starting values of a new appointment:
        /// <c>duration</c> (minutes), <c>starttime</c> (<c>HH:mm</c>), <c>allday</c>,
        /// <c>location</c>, <c>recurrence</c>, <c>participants</c>, <c>resources</c> and
        /// <c>autoclose</c>, besides the title and description of the template.
        /// </summary>
        /// <param name="values">The starting values.</param>
        /// <param name="presets">The presets of the template.</param>
        /// <param name="zone">The zone of the new appointment.</param>
        /// <param name="culture">The culture of the request.</param>
        public static void ApplyPresets(Dictionary<string, object> values, IReadOnlyDictionary<string, string> presets, TimeZoneInfo zone, CultureInfo culture)
        {
            if (presets is null || presets.Count == 0)
            {
                return;
            }

            if (presets.TryGetValue(nameof(ObjectEntity.Summary), out var summary) && !string.IsNullOrWhiteSpace(summary))
            {
                values[Summary] = summary;
            }

            if (presets.TryGetValue(nameof(ObjectEntity.Description), out var description) && !string.IsNullOrWhiteSpace(description))
            {
                values[Description] = description;
            }

            var calendar = AppointmentObjects.CalendarPresets(presets);
            var day = DateTime.ParseExact((string)values[StartDate], culture.DateTimeFormat.ShortDatePattern, culture);
            var startTime = TryParseTime((string)values[StartTime], out var parsedStart) ? parsedStart : TimeSpan.FromHours(9);

            if (calendar.TryGetValue("starttime", out var presetTime) && TryParseTime(presetTime, out var time))
            {
                startTime = time;
            }

            var duration = calendar.TryGetValue("duration", out var minutesText) && int.TryParse(minutesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes) && minutes > 0
                ? TimeSpan.FromMinutes(Math.Min(minutes, 60 * 24 * 31))
                : TimeSpan.FromHours(1);

            var localStart = day.Date + startTime;
            var localEnd = localStart + duration;

            values[StartTime] = localStart.ToString("HH:mm", CultureInfo.InvariantCulture);
            values[EndDate] = localEnd.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
            values[EndTime] = localEnd.ToString("HH:mm", CultureInfo.InvariantCulture);

            if (calendar.TryGetValue("allday", out var allDay))
            {
                values[AllDay] = IsTrue(allDay);
            }

            if (calendar.TryGetValue("location", out var location))
            {
                values[Location] = location ?? string.Empty;
            }

            if (calendar.TryGetValue("autoclose", out var autoClose))
            {
                values[AutoClose] = IsTrue(autoClose);
            }

            if (calendar.TryGetValue("recurrence", out var rule))
            {
                foreach (var (key, value) in Recurrence(rule, culture, zone))
                {
                    values[key] = value;
                }
            }

            if (calendar.TryGetValue("participants", out var participants))
            {
                values[Participants] = SplitIds(participants).Select(x => x.ToString()).ToList();
            }

            if (calendar.TryGetValue("resources", out var resources))
            {
                values[Resources] = SplitIds(resources).Select(x => x.ToString()).ToList();
            }
        }

        /// <summary>
        /// Reads a submitted dialog.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="culture">The culture of the request.</param>
        /// <param name="fallbackZone">The zone when the dialog names none.</param>
        /// <param name="errors">Receives the internationalization keys of what is wrong.</param>
        /// <returns>The draft; meaningful only when no error was added.</returns>
        public static AppointmentDraft Read(FormPayload payload, CultureInfo culture, TimeZoneInfo fallbackZone, ICollection<string> errors)
        {
            culture ??= CultureInfo.CurrentCulture;

            var draft = new AppointmentDraft
            {
                Summary = payload.Text(Summary),
                Description = payload.Has(Description) ? payload.Raw(Description) ?? string.Empty : null,
                AllDay = payload.Flag(AllDay),
                Location = payload.Text(Location),
                AutoClose = payload.Flag(AutoClose, true),
                IgnoreConflicts = payload.Flag(IgnoreConflicts)
            };

            if (string.IsNullOrWhiteSpace(draft.Summary))
            {
                errors.Add("kleenestar.calendar:appointment.summary.required");
            }

            var zone = ParseZone(payload.Text(TimeZone)) ?? fallbackZone ?? TimeZoneInfo.Local;
            draft.TimeZone = zone.Id;

            if (!TryParseDay(payload.Text(StartDate), culture, out var startDay))
            {
                errors.Add("kleenestar.calendar:appointment.start.required");

                return draft;
            }

            var endDay = TryParseDay(payload.Text(EndDate), culture, out var parsedEndDay) ? parsedEndDay : startDay;

            if (draft.AllDay)
            {
                if (endDay < startDay)
                {
                    errors.Add("kleenestar.calendar:appointment.end.before");
                }

                draft.Start = AppointmentTime.ToUtc(startDay, zone);
                draft.End = AppointmentTime.ToUtc(endDay.AddDays(1), zone);
            }
            else
            {
                var startTime = TryParseTime(payload.Text(StartTime), out var parsedStart) ? parsedStart : TimeSpan.FromHours(9);
                var hasEndTime = TryParseTime(payload.Text(EndTime), out var parsedEnd);
                var localStart = startDay + startTime;
                var localEnd = hasEndTime ? endDay + parsedEnd : localStart.AddHours(1);

                if (localEnd < localStart)
                {
                    errors.Add("kleenestar.calendar:appointment.end.before");
                }

                draft.Start = AppointmentTime.ToUtc(localStart, zone);
                draft.End = AppointmentTime.ToUtc(localEnd, zone);
            }

            draft.Recurrence = ReadRecurrence(payload, culture, zone, errors);

            draft.IdentityIds.AddRange(payload.Ids(Participants));
            draft.Guests.AddRange(ParseGuests(payload.Raw(Guests)));
            draft.ResourceIds.AddRange(payload.Ids(Resources));

            return draft;
        }

        /// <summary>
        /// Reads the series a dialog asks for.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <param name="culture">The culture of the request.</param>
        /// <param name="zone">The zone of the appointment.</param>
        /// <param name="errors">Receives what is wrong.</param>
        /// <returns>The rule, or <see langword="null"/> for a single appointment.</returns>
        private static string ReadRecurrence(FormPayload payload, CultureInfo culture, TimeZoneInfo zone, ICollection<string> errors)
        {
            var written = payload.Text(Rule);
            var frequency = (payload.Text(Repeat) ?? "none").ToLowerInvariant();

            if (frequency == "custom" || (!string.IsNullOrWhiteSpace(written) && frequency == "none"))
            {
                if (string.IsNullOrWhiteSpace(written))
                {
                    return null;
                }

                if (!RecurrenceRule.TryParse(written, out var custom, out _))
                {
                    errors.Add("kleenestar.calendar:appointment.rule.invalid");
                    return null;
                }

                return custom.ToString();
            }

            var parsed = frequency switch
            {
                "daily" => RecurrenceFrequency.Daily,
                "weekly" => RecurrenceFrequency.Weekly,
                "monthly" => RecurrenceFrequency.Monthly,
                "yearly" => RecurrenceFrequency.Yearly,
                _ => (RecurrenceFrequency?)null
            };

            if (parsed is null)
            {
                return null;
            }

            var interval = Math.Clamp(payload.Number(Interval) ?? 1, 1, 1000);
            var count = payload.Number(Count);
            DateTime? until = null;

            if (TryParseDay(payload.Text(Until), culture, out var untilDay))
            {
                // the series ends with the last day named, in the zone it repeats in
                until = AppointmentTime.ToUtc(untilDay.AddDays(1), zone).AddSeconds(-1);
            }

            var days = parsed == RecurrenceFrequency.Weekly
                ? payload.List(Weekdays)
                    .Select(x => x.ToUpperInvariant())
                    .Where(Days.Contains)
                    .Select(x => RecurrenceRule.Parse("FREQ=WEEKLY;BYDAY=" + x).ByDay[0])
                    .ToList()
                : [];

            var rule = new RecurrenceRule
            {
                Frequency = parsed.Value,
                Interval = interval,
                Count = count is > 0 ? Math.Min(count.Value, RecurrenceRule.MaxOccurrences) : null,
                Until = count is > 0 ? null : until,
                ByDay = days
            };

            return rule.ToString();
        }

        /// <summary>
        /// Returns the series inputs of a rule: the simple ones where they can express it, the
        /// rule written out otherwise.
        /// </summary>
        /// <param name="text">The rule, or <see langword="null"/>.</param>
        /// <param name="culture">The culture of the request.</param>
        /// <param name="zone">The zone of the appointment.</param>
        /// <returns>The values.</returns>
        private static IEnumerable<(string Key, object Value)> Recurrence(string text, CultureInfo culture, TimeZoneInfo zone)
        {
            var rule = RecurrenceRule.Parse(text);

            if (rule is null)
            {
                return [(Repeat, "none"), (Interval, "1"), (Weekdays, new List<string>()), (Until, string.Empty), (Count, string.Empty), (Rule, string.Empty)];
            }

            var simple = rule.ByMonth.Count == 0
                && rule.ByMonthDay.Count == 0
                && rule.ByDay.All(x => x.Ordinal == 0)
                && (rule.ByDay.Count == 0 || rule.Frequency == RecurrenceFrequency.Weekly);

            var until = rule.Until is null
                ? string.Empty
                : AppointmentTime.ToLocal(rule.Until.Value, zone).ToString(culture.DateTimeFormat.ShortDatePattern, culture);

            return
            [
                (Repeat, simple ? rule.Frequency.ToString().ToLowerInvariant() : "custom"),
                (Interval, rule.Interval.ToString(CultureInfo.InvariantCulture)),
                (Weekdays, rule.ByDay.Select(x => RecurrenceRule.Token(x.Day)).Distinct().ToList()),
                (Until, until),
                (Count, rule.Count?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                (Rule, simple ? string.Empty : rule.ToString())
            ];
        }

        /// <summary>
        /// Reads the guests of the dialog: entries separated by semicolons or lines, each a name,
        /// a mail address or <c>Name &lt;mail&gt;</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The guests.</returns>
        public static IEnumerable<AppointmentParticipant> ParseGuests(string text)
        {
            foreach (var entry in (text ?? string.Empty).Split([';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var match = Regex.Match(entry, "^(?<name>.*?)\\s*<(?<mail>[^>]+)>$");
                string name;
                string mail;

                if (match.Success)
                {
                    name = match.Groups["name"].Value.Trim().Trim('"');
                    mail = match.Groups["mail"].Value.Trim();
                }
                else if (entry.Contains('@') && !entry.Contains(' '))
                {
                    name = null;
                    mail = entry;
                }
                else
                {
                    name = entry;
                    mail = null;
                }

                yield return new AppointmentParticipant
                {
                    Name = string.IsNullOrWhiteSpace(name) ? mail : name,
                    Email = string.IsNullOrWhiteSpace(mail) ? null : mail,
                    Role = AppointmentParticipantRole.Required
                };
            }
        }

        /// <summary>
        /// Writes a guest as the dialog shows it.
        /// </summary>
        /// <param name="participant">The guest.</param>
        /// <returns>The text.</returns>
        public static string FormatGuest(AppointmentParticipant participant)
        {
            if (string.IsNullOrWhiteSpace(participant.Email))
            {
                return participant.Name;
            }

            return string.IsNullOrWhiteSpace(participant.Name) || string.Equals(participant.Name, participant.Email, StringComparison.OrdinalIgnoreCase)
                ? participant.Email
                : $"{participant.Name} <{participant.Email}>";
        }

        /// <summary>
        /// Answers the option id of a zone in the zone selection.
        /// </summary>
        /// <param name="zone">The zone.</param>
        /// <returns>The id.</returns>
        public static string ZoneOption(TimeZoneInfo zone)
        {
            if (zone is null)
            {
                return null;
            }

            if (zone.HasIanaId)
            {
                return zone.Id;
            }

            return TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var iana) ? iana : zone.Id;
        }

        /// <summary>
        /// Reads a zone of the dialog.
        /// </summary>
        /// <param name="text">The id.</param>
        /// <returns>The zone, or <see langword="null"/>.</returns>
        private static TimeZoneInfo ParseZone(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : AppointmentTime.Zone(text);
        }

        /// <summary>
        /// Reads a day of a date input.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="culture">The culture of the request.</param>
        /// <param name="day">The day.</param>
        /// <returns><see langword="true"/> when a day was read.</returns>
        public static bool TryParseDay(string text, CultureInfo culture, out DateTime day)
        {
            day = default;

            if (!Core.WebControl.FieldValueForm.TryParseDate(text, culture, out var date))
            {
                return false;
            }

            day = DateTime.SpecifyKind(date.Date, DateTimeKind.Unspecified);

            return true;
        }

        /// <summary>
        /// Reads a time of day (<c>14:30</c>, <c>14.30</c>, <c>9:05</c>).
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="time">The time.</param>
        /// <returns><see langword="true"/> when a time was read.</returns>
        public static bool TryParseTime(string text, out TimeSpan time)
        {
            time = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var match = Regex.Match(text.Trim(), "^(?<h>[0-9]{1,2})[:.](?<m>[0-9]{2})");

            if (!match.Success)
            {
                return false;
            }

            var hours = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
            var minutes = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);

            if (hours > 23 || minutes > 59)
            {
                return false;
            }

            time = new TimeSpan(hours, minutes, 0);

            return true;
        }

        /// <summary>
        /// Answers the next full hour from now in a zone, in UTC.
        /// </summary>
        /// <param name="zone">The zone.</param>
        /// <returns>The instant.</returns>
        public static DateTime NextHour(TimeZoneInfo zone)
        {
            var local = AppointmentTime.ToLocal(DateTime.UtcNow, zone);

            return AppointmentTime.ToUtc(local.Date.AddHours(local.Hour + 1), zone);
        }

        /// <summary>
        /// Reads ids separated by semicolons or commas.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The ids.</returns>
        public static IEnumerable<Guid> SplitIds(string text)
        {
            return (text ?? string.Empty)
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
                .Where(x => x != Guid.Empty)
                .Distinct();
        }

        /// <summary>
        /// Reads a stored boolean.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The value.</returns>
        private static bool IsTrue(string text)
        {
            return (text ?? string.Empty).Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on";
        }

        /// <summary>
        /// Writes the participants a draft names: the accounts, the organizer among them as the
        /// chair, and the guests.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <param name="organizerId">The organizer, or <see langword="null"/>.</param>
        /// <param name="stored">The participants stored so far, whose roles stay.</param>
        /// <returns>The participants.</returns>
        public static List<AppointmentParticipant> ParticipantsOf(AppointmentDraft draft, Guid? organizerId, IReadOnlyList<AppointmentParticipant> stored)
        {
            var result = new List<AppointmentParticipant>();
            var ids = draft.IdentityIds.ToList();

            if (organizerId is Guid organizer && organizer != Guid.Empty && !ids.Contains(organizer) && !(stored ?? []).Any())
            {
                // whoever plans a new appointment attends it
                ids.Insert(0, organizer);
            }

            foreach (var id in ids.Distinct())
            {
                var before = stored?.FirstOrDefault(x => x.IdentityId == id);
                var identity = CoreHub.IdentityManager?.GetIdentity(id);

                if (identity is null && before is null)
                {
                    continue;
                }

                result.Add(new AppointmentParticipant
                {
                    IdentityId = id,
                    Name = identity?.Name ?? before?.Name,
                    Role = before?.Role ?? (id == organizerId ? AppointmentParticipantRole.Chair : AppointmentParticipantRole.Required),
                    Response = id == organizerId && before is null ? AppointmentResponse.Accepted : AppointmentResponse.Pending,
                    Responded = id == organizerId && before is null ? DateTime.UtcNow : null
                });
            }

            result.AddRange(draft.Guests);

            return result;
        }
    }
}
