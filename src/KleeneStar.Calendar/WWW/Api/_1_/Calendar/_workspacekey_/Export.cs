using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core.WebReport;
using System;
using System.Linq;
using System.Text;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// Downloads the calendar of a workspace as an iCalendar file (<c>{key}.ics</c>): every
    /// appointment the caller may see, a series with its rule and exceptions, and with
    /// <c>?layers=true</c> also what the layers show in the year around today.
    /// </summary>
    /// <remarks>
    /// The address can be subscribed to by a calendar that signs in like a browser; the
    /// identifiers stay the same between downloads, so a subscription updates in place.
    /// </remarks>
    [Title("kleenestar.calendar:export.label")]
    [Cache]
    public sealed class Export : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Export()
        {
        }

        /// <summary>
        /// Answers the file.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The attachment.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var layers = string.Equals(request.GetParameter("layers")?.Value, "true", StringComparison.OrdinalIgnoreCase);
            var events = AppointmentExchange.Events(workspace, layers);
            var content = ICalendar.Write(events, workspace.Name);
            var name = string.Concat((workspace.Key ?? "calendar").Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_')) + ".ics";

            return ObjectExport.Attachment(Encoding.UTF8.GetBytes(content), ICalendar.ContentType + "; charset=utf-8", name);
        }
    }
}
