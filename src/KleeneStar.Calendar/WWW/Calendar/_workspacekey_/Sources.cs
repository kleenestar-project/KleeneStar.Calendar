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
    /// The page behind the dialog including another workspace or a WQL query as a layer of the calendar. It is opened as a modal; its form is a fragment scoped to it.
    /// </summary>
    [WebIcon<IconLayerGroup>]
    [Title("kleenestar.calendar:source.add.title")]
    [Scope<IScopeGeneral>]
    public sealed class Sources : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Sources()
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

            AppointmentAuthorization.DemandArrange(CalendarRequest.Workspace(request), request);
        }
    }
}
