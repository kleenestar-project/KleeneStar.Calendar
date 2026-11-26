using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_
{
    /// <summary>
    /// The endpoint of the dialog removing one occurrence from a series: a GET answers the coming
    /// occurrences the dialog offers, a POST removes the chosen one.
    /// </summary>
    [Title("kleenestar.calendar:exclude.title")]
    [Cache]
    public sealed class Exclude : IRestApi
    {
        /// <summary>Input name of the occurrence.</summary>
        public const string OccurrenceField = "Occurrence";

        /// <summary>
        /// The most occurrences the dialog offers.
        /// </summary>
        public const int MaxOffered = 60;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Exclude()
        {
        }

        /// <summary>
        /// Answers the occurrences the dialog offers: the coming ones, the next first.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = CalendarRequest.Appointment(request);
            var schedule = entity is null ? null : CalendarHub.AppointmentManager.GetSchedule(entity.Id);

            if (schedule is null)
            {
                return new ResponseNotFound();
            }

            var occurrences = Offered(schedule, request);

            return FormAnswer.Json(new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object> { [OccurrenceField] = occurrences.FirstOrDefault().Id ?? string.Empty },
                ["occurrences"] = occurrences.Select(x => new { id = x.Id, text = x.Text })
            });
        }

        /// <summary>
        /// Removes the chosen occurrence.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Remove(IRequest request)
        {
            var entity = CalendarRequest.Appointment(request);

            if (entity is null)
            {
                return new ResponseNotFound();
            }

            if (!AppointmentAuthorization.MayWrite(entity, request))
            {
                return new ResponseForbidden();
            }

            var chosen = FormPayload.Read(request).Text(OccurrenceField) ?? request.GetParameter("occurrence")?.Value;
            var start = AppointmentSchedule.ParseInstant(chosen);

            if (start is null || !CalendarHub.AppointmentManager.ExcludeOccurrence(entity.Id, start.Value))
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [OccurrenceField] = "kleenestar.calendar:exclude.required" });
            }

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:exclude.done", CalendarRequest.FormatSpan(start.Value, start.Value, false, request)));

            return FormAnswer.Success();
        }

        /// <summary>
        /// Answers the occurrences offered: the next ones from today, written in the caller's
        /// zone and culture.
        /// </summary>
        /// <param name="schedule">The schedule.</param>
        /// <param name="request">The request.</param>
        /// <returns>The occurrences, each with its id (its start) and text.</returns>
        public static IReadOnlyList<(string Id, string Text)> Offered(Model.Entities.Appointment schedule, IRequest request)
        {
            var from = DateTime.UtcNow.Date;

            return [.. AppointmentSchedule
                .Occurrences(schedule, from, from.AddYears(5), MaxOffered)
                .Where(x => x.Recurring)
                .Select(x => (AppointmentSchedule.FormatInstant(x.Start), CalendarRequest.FormatSpan(x.Start, x.End, x.AllDay, request)))];
        }
    }
}
