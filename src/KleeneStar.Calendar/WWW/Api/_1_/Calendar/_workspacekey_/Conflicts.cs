using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// Answers what a planned booking collides with, for the conflict panel of the appointment
    /// dialog: the dialog posts its inputs (and <c>Appointment</c>, the key of the appointment
    /// being changed) whenever the time, the series, the participants or the resources change.
    /// </summary>
    /// <remarks>
    /// The answer is <c>{ conflicts: [ { kind, subject, text, uri } ], checked }</c>. A collision
    /// with an appointment the caller may not see names the time only.
    /// </remarks>
    [Title("kleenestar.calendar:conflict.title")]
    [Cache]
    public sealed class Conflicts : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Conflicts()
        {
        }

        /// <summary>
        /// Answers the conflicts of the posted dialog.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The conflicts.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Check(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var payload = FormPayload.Read(request);
            var errors = new List<string>();
            var draft = AppointmentForm.Read(payload, CalendarRequest.Culture(request), CalendarRequest.Zone(request), errors);

            // a dialog without a readable time has nothing to collide with yet
            if (errors.Contains("kleenestar.calendar:appointment.start.required") || errors.Contains("kleenestar.calendar:appointment.end.before"))
            {
                return FormAnswer.Json(new { conflicts = Array.Empty<object>(), @checked = false });
            }

            var key = payload.Text("Appointment");
            var current = string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);

            var conflicts = AppointmentPlanning.Conflicts(draft, current?.Id)
                .Select(x => new Dictionary<string, object>
                {
                    ["kind"] = x.Kind == AppointmentConflictKind.Resource ? "resource" : "participant",
                    ["subject"] = x.SubjectName,
                    ["text"] = AppointmentPlanning.Describe(x, request),
                    ["uri"] = x.Other is null ? null : CoreHub.ObjectKindManager.ResolveDetailUri(x.Other)?.ToString()
                })
                .ToList();

            return FormAnswer.Json(new { conflicts, @checked = true });
        }
    }
}
