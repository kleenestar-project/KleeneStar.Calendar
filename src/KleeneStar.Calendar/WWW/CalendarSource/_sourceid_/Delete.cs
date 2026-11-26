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

namespace KleeneStar.Calendar.WWW.CalendarSource._sourceid_
{
    /// <summary>
    /// The page behind the dialog removing a layer from a workspace calendar. It is opened as a modal; its form is a fragment scoped to it.
    /// </summary>
    [WebIcon<IconTrash>]
    [Title("kleenestar.calendar:source.delete.title")]
    [Scope<IScopeGeneral>]
    public sealed class Delete : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Delete()
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

            var source = CalendarHub.AppointmentManager.GetSource(Guid.TryParse(request.GetParameter<SourceIdParameter>()?.Value, out var id) ? id : Guid.Empty);

            if (source is not null)
            {
                AppointmentAuthorization.DemandArrange(CoreHub.WorkspaceManager.GetWorkspace(source.WorkspaceId), request);
            }
        }
    }
}
