using KleeneStar.Core;
using KleeneStar.Core.WebReport;
using System.Text;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The iCalendar format of the core's export dialog: an appointment as an event with its
    /// schedule, series, place, participants and answers; any other object as an all-day event
    /// placed by the date fields of its class, so that a due date reaches a personal calendar.
    /// </summary>
    /// <remarks>
    /// The core's <c>ObjectExportManager</c> finds the format in the plugin by its type; the
    /// object is read again by key through the object manager, so its visibility applies as it
    /// does for every format.
    /// </remarks>
    public sealed class IcsObjectExportFormat : IObjectExportFormat
    {
        /// <inheritdoc/>
        public string Key => "ics";

        /// <inheritdoc/>
        public string Extension => "ics";

        /// <inheritdoc/>
        public string Label => "kleenestar.calendar:export.format.label";

        /// <inheritdoc/>
        public string Description => "kleenestar.calendar:export.format.description";

        /// <inheritdoc/>
        public string Content => "kleenestar.calendar:export.format.content";

        /// <inheritdoc/>
        public string Badge => "ICS";

        /// <inheritdoc/>
        public TypeColorTile Color => TypeColorTile.Success;

        /// <inheritdoc/>
        public IIcon Icon => new IconCalendarDays();

        /// <inheritdoc/>
        public int Order => 40;

        /// <inheritdoc/>
        public IResponse Write(ObjectExportRecord record, IRequest request)
        {
            var entity = CoreHub.ObjectManager.GetObjectByKey(record?.Key);
            var events = entity is null ? [] : AppointmentExchange.Events([entity]);
            var content = ICalendar.Write(events, entity?.Summary);

            return ObjectExport.Attachment(Encoding.UTF8.GetBytes(content), ICalendar.ContentType + "; charset=utf-8", ObjectExport.FileName(record, Extension));
        }
    }
}
