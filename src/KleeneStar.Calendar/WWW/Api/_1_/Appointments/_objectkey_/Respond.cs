using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using KleeneStar.Model.Entities;
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
    /// The endpoint of the answer of an invited account: a GET answers the answer given so far,
    /// a POST writes the new one - accepted, accepted with reservations, or declined - with the
    /// reservation or reason in the participant's words. A quick answer from the participants
    /// card posts <c>?response=</c> without dialog.
    /// </summary>
    [Title("kleenestar.calendar:respond.title")]
    [Cache]
    public sealed class Respond : IRestApi
    {
        /// <summary>Input name of the answer.</summary>
        public const string ResponseField = "Response";

        /// <summary>Input name of the reservation or reason.</summary>
        public const string CommentField = "Comment";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Respond()
        {
        }

        /// <summary>
        /// Answers the answer given so far.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var entity = CalendarRequest.Appointment(request);
            var identityId = CalendarRequest.Identity(request);
            var participant = entity is null ? null : CalendarHub.AppointmentManager.GetParticipants(entity.Id).FirstOrDefault(x => x.IdentityId == identityId);

            if (participant is null)
            {
                return new ResponseNotFound();
            }

            // the dialog opened from one of the answer buttons names that answer as its id
            var asked = Enum.TryParse<AppointmentResponse>(request.GetParameter("id")?.Value, true, out var clicked) && clicked != AppointmentResponse.Pending
                ? clicked
                : participant.Response == AppointmentResponse.Pending ? AppointmentResponse.Accepted : participant.Response;

            return FormAnswer.Values(new Dictionary<string, object>
            {
                [ResponseField] = asked.ToString().ToLowerInvariant(),
                [CommentField] = participant.Comment ?? string.Empty
            });
        }

        /// <summary>
        /// Writes the answer.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>A success, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Answer(IRequest request)
        {
            var entity = CalendarRequest.Appointment(request);

            if (entity is null)
            {
                return new ResponseNotFound();
            }

            if (!AppointmentAuthorization.MayRespond(entity, request))
            {
                return new ResponseForbidden();
            }

            var payload = FormPayload.Read(request);
            var text = payload.Text(ResponseField) ?? request.GetParameter("response")?.Value;

            if (!Enum.TryParse<AppointmentResponse>(text, true, out var response) || response == AppointmentResponse.Pending)
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [ResponseField] = "kleenestar.calendar:respond.required" });
            }

            var comment = payload.Has(CommentField) ? payload.Text(CommentField) : null;

            // a reservation without words is no reservation, so a tentative answer asks for one
            if (response == AppointmentResponse.Tentative && string.IsNullOrWhiteSpace(comment) && payload.Has(CommentField))
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [CommentField] = "kleenestar.calendar:respond.reservation.required" });
            }

            CalendarHub.AppointmentManager.Respond(entity.Id, CalendarRequest.Identity(request), response, comment);
            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:respond.done." + response.ToString().ToLowerInvariant(), entity.Summary));

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString());
        }
    }
}
