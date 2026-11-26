using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// Imports the events of an iCalendar file into the calendar of a workspace. The dialog
    /// posts the class new appointments are created in and the content of the file (read in the
    /// browser, because a form posts no file); an API client may as well post the file itself
    /// as <c>text/calendar</c>, with the class as <c>?class=</c>.
    /// </summary>
    /// <remarks>
    /// An event imported before (same <c>UID</c>) updates its appointment when the caller may
    /// change it. The answer reports how many appointments were created, updated and skipped.
    /// </remarks>
    [Title("kleenestar.calendar:import.title")]
    [Cache]
    public sealed class Import : IRestApi
    {
        /// <summary>
        /// The input name of the class.
        /// </summary>
        public const string ClassField = "ClassId";

        /// <summary>
        /// The input name of the file content.
        /// </summary>
        public const string ContentField = "Content";

        /// <summary>
        /// The input name of the file chooser.
        /// </summary>
        public const string FileField = "File";

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Import()
        {
        }

        /// <summary>
        /// Answers the values the dialog starts from: the first class the caller may create in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var @class = CalendarRequest.Classes(workspace).FirstOrDefault(x => AppointmentAuthorization.MayCreate(x, request));

            return FormAnswer.Values(new Dictionary<string, object>
            {
                [ClassField] = @class?.Id.ToString() ?? string.Empty,
                [ContentField] = string.Empty
            });
        }

        /// <summary>
        /// Imports the file.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The outcome, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Create(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            string text;
            System.Guid? classId;

            if (request.Header?.ContentType?.Contains("text/calendar", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                text = Encoding.UTF8.GetString((request as Request)?.Content ?? []).TrimStart('﻿');
                classId = System.Guid.TryParse(request.GetParameter("class")?.Value, out var parsed) ? parsed : null;
            }
            else
            {
                var payload = FormPayload.Read(request);
                text = payload.File(FileField) ?? payload.Raw(ContentField);
                classId = payload.Id(ClassField);
            }

            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request)).ToList();
            var @class = classes.FirstOrDefault(x => x.Id == classId) ?? (classId is null ? classes.FirstOrDefault() : null);

            if (@class is null)
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [ClassField] = "kleenestar.calendar:appointment.class.required" });
            }

            if (string.IsNullOrWhiteSpace(text) || !text.Contains("BEGIN:VEVENT", System.StringComparison.OrdinalIgnoreCase))
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [ContentField] = "kleenestar.calendar:import.empty" });
            }

            var result = AppointmentExchange.Import
            (
                workspace,
                @class,
                text,
                CalendarRequest.Identity(request),
                x => AppointmentAuthorization.MayWrite(x, request),
                CalendarRequest.Zone(request)
            );

            var message = I18N.Translate(request, "kleenestar.calendar:import.done", result.Created, result.Updated, result.Skipped);

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), message);

            return FormAnswer.Success
            (
                CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>()?.BindParameters(request)?.ToString(),
                new Dictionary<string, object>
                {
                    ["created"] = result.Created,
                    ["updated"] = result.Updated,
                    ["skipped"] = result.Skipped
                }
            );
        }
    }
}
