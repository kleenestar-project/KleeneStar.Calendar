using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebInclude;

namespace KleeneStar.Calendar.WebInclude
{
    /// <summary>
    /// Includes the conflict panel of the appointment dialog on the pages it is opened from: the
    /// calendar (planning a new appointment) and the appointment page (changing one).
    /// </summary>
    [Asset("/assets/js/kleenestar.calendar.conflicts.js")]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Add>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Schedule>]
    public sealed class IncludeCalendarConflictsScript : IInclude
    {
    }

    /// <summary>
    /// Includes the file reader of the import dialog on the calendar it is opened from.
    /// </summary>
    [Asset("/assets/js/kleenestar.calendar.import.js")]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Import>]
    public sealed class IncludeCalendarImportScript : IInclude
    {
    }
}
