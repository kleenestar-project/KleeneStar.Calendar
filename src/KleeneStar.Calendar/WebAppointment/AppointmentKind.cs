using KleeneStar.Core;
using KleeneStar.Core.WebFragment.Object;
using KleeneStar.Core.WebParameter;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The appointment kind: meetings and dates of a workspace, shown in its calendar. The
    /// overview is the calendar (<c>/calendar/{workspacekey}</c>), the detail view the
    /// appointment page (<c>/appointment/{objectkey}</c>), which carries the core's full object
    /// detail experience beside the schedule, the participants and the bookings.
    /// </summary>
    public sealed class AppointmentKind : IObjectKind
    {
        /// <summary>
        /// Gets the persisted kind key of appointments.
        /// </summary>
        public string Key => Model.Entities.ObjectKind.Appointment;

        /// <summary>
        /// Gets the internationalization key of the display name: the calendar, which is how the
        /// kind appears in the sidebar.
        /// </summary>
        public string Label => "kleenestar.calendar:kind.label";

        /// <summary>
        /// Gets the icon representing appointments.
        /// </summary>
        public IIcon Icon => new IconCalendarDays();

        /// <summary>
        /// Gets the display order; the calendar sits between the blogs (2) and the issues (4).
        /// </summary>
        public int Order => 3;

        /// <summary>
        /// Gets <see langword="false"/>: an appointment takes place, nobody waits on it, so no
        /// SLA times it.
        /// </summary>
        public bool ServiceLevels => false;

        /// <summary>
        /// Gets the unbound route of the calendar of a workspace.
        /// </summary>
        public IUri OverviewUri => CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>();

        /// <summary>
        /// Returns the appointment page bound to the supplied object key.
        /// </summary>
        /// <param name="objectKey">The key of the appointment.</param>
        /// <returns>The bound detail route.</returns>
        public IUri DetailUri(string objectKey) => CoreHub
            .GetUri<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>()?
            .BindParameters(new ObjectKeyParameter(objectKey));

        /// <summary>
        /// Returns <see langword="null"/>: an appointment is edited through dialogs opened from
        /// its page.
        /// </summary>
        /// <param name="objectKey">The key of the appointment (unused).</param>
        /// <returns>Always <see langword="null"/>.</returns>
        public IUri EditUri(string objectKey) => null;
    }

    /// <summary>
    /// Gates the sidebar link of the calendar: it is shown where the workspace has a class of
    /// the appointment kind.
    /// </summary>
    public sealed class AppointmentKindConfiguredCondition : ObjectKindConfiguredCondition
    {
        /// <inheritdoc/>
        protected override string Kind => Model.Entities.ObjectKind.Appointment;
    }
}
