using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebParameter;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WWW.Calendar._workspacekey_
{
    /// <summary>
    /// The page behind the dialog importing the events of an iCalendar file into the calendar. It is opened as a modal; its form is a fragment scoped to it.
    /// </summary>
    [WebIcon<IconFileImport>]
    [Title("kleenestar.calendar:import.title")]
    [Scope<IScopeGeneral>]
    public sealed class Import : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Import()
        {
        }

        /// <summary>
        /// Refuses a caller who may not use the dialog.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;

            AppointmentAuthorization.DemandRead(CalendarRequest.Workspace(request), request);
        }
    }
}
