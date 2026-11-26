using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// Describes a recurrence rule in words: <c>Weekly on Mon, Wed · 10 times</c>,
    /// <c>Every 2 months on day 15 · until 12/31/2026</c>.
    /// </summary>
    public static class RecurrenceText
    {
        /// <summary>
        /// Describes a rule in the language of a request.
        /// </summary>
        /// <param name="text">The rule.</param>
        /// <param name="request">The request.</param>
        /// <param name="zone">The zone the end of the series is shown in.</param>
        /// <returns>The description, or <see langword="null"/> for no rule.</returns>
        public static string Describe(string text, IRequest request, System.TimeZoneInfo zone)
        {
            var rule = RecurrenceRule.Parse(text);

            if (rule is null)
            {
                return null;
            }

            var culture = CalendarRequest.Culture(request);
            var unit = rule.Frequency.ToString().ToLowerInvariant();
            var parts = new List<string>
            {
                rule.Interval == 1
                    ? I18N.Translate(request, $"kleenestar.calendar:recurrence.{unit}")
                    : I18N.Translate(request, $"kleenestar.calendar:recurrence.{unit}.every", rule.Interval)
            };

            if (rule.ByDay.Count > 0)
            {
                var days = rule.ByDay.Select(x =>
                {
                    var name = culture.DateTimeFormat.AbbreviatedDayNames[(int)x.Day];

                    return x.Ordinal switch
                    {
                        0 => name,
                        -1 => I18N.Translate(request, "kleenestar.calendar:recurrence.last", name),
                        _ => $"{x.Ordinal}. {name}"
                    };
                });

                parts.Add(I18N.Translate(request, "kleenestar.calendar:recurrence.on", string.Join(", ", days)));
            }

            if (rule.ByMonthDay.Count > 0)
            {
                parts.Add(I18N.Translate(request, "kleenestar.calendar:recurrence.day", string.Join(", ", rule.ByMonthDay)));
            }

            if (rule.ByMonth.Count > 0)
            {
                parts.Add(I18N.Translate(request, "kleenestar.calendar:recurrence.in", string.Join(", ", rule.ByMonth.Select(x => culture.DateTimeFormat.GetAbbreviatedMonthName(x)))));
            }

            var description = string.Join(" ", parts);

            if (rule.Count is not null)
            {
                description += " · " + I18N.Translate(request, "kleenestar.calendar:recurrence.count", rule.Count.Value);
            }
            else if (rule.Until is not null)
            {
                var until = AppointmentTime.ToLocal(rule.Until.Value, zone).ToString(culture.DateTimeFormat.ShortDatePattern, culture);
                description += " · " + I18N.Translate(request, "kleenestar.calendar:recurrence.until", until);
            }

            return description;
        }
    }
}
