using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_
{
    /// <summary>
    /// The endpoint of the dialog changing an appointment: a GET answers its title, description,
    /// schedule, series, participants and resources as the dialog shows them; a PUT (or POST)
    /// writes what the dialog submits.
    /// </summary>
    [Title("kleenestar.calendar:appointment.edit.title")]
    [ObjectKeySegment]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the values the dialog starts from.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = CalendarRequest.Appointment(request);

            if (entity is null)
            {
                return new ResponseNotFound();
            }

            var manager = CalendarHub.AppointmentManager;

            return FormAnswer.Values(AppointmentForm.Values
            (
                entity,
                manager.GetSchedule(entity.Id),
                manager.GetParticipants(entity.Id),
                manager.GetResources(entity.Id),
                CalendarRequest.Zone(request),
                CalendarRequest.Culture(request)
            ));
        }

        /// <summary>
        /// Writes the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.PUT)]
        [Method(RequestMethod.PATCH)]
        public IResponse Update(IRequest request)
        {
            return Save(request);
        }

        /// <summary>
        /// Writes the dialog (a form that posts).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Post(IRequest request)
        {
            return Save(request);
        }

        /// <summary>
        /// Writes the dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        private static IResponse Save(IRequest request)
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

            var keys = new List<string>();
            var draft = AppointmentForm.Read(FormPayload.Read(request), CalendarRequest.Culture(request), CalendarRequest.Zone(request), keys);

            if (keys.Count > 0)
            {
                return FormAnswer.Errors(request, global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Appointment.ErrorsOf(keys));
            }

            var errors = AppointmentPlanning.Check(draft, entity.Id, request);

            if (errors.Count > 0)
            {
                return FormAnswer.Errors(request, errors);
            }

            AppointmentPlanning.Update(entity, draft, CalendarRequest.Identity(request));

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:appointment.edit.done", entity.Key, entity.Summary));

            return FormAnswer.Success();
        }
    }
}
