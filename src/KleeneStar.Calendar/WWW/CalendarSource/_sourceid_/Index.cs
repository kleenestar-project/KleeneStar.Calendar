using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebParameter;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WWW.CalendarSource._sourceid_
{
    /// <summary>
    /// The address of a layer of a workspace calendar (<c>/calendarsource/{sourceid}</c>). It
    /// carries the route segment of the folder's dialogs and leads to the calendar the layer
    /// belongs to.
    /// </summary>
    [SegmentGuid<SourceIdParameter>]
    [WebIcon<IconLayerGroup>]
    [Title("kleenestar.calendar:source.label")]
    [Scope<IScopeGeneral>]
    public sealed class Index : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Leads to the calendar the layer belongs to.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;
            var source = Guid.TryParse(request.GetParameter<SourceIdParameter>()?.Value, out var id)
                ? CalendarHub.AppointmentManager.GetSource(id)
                : null;
            var workspace = source is null ? null : CoreHub.WorkspaceManager.GetWorkspace(source.WorkspaceId);

            if (workspace is null)
            {
                return;
            }

            AppointmentAuthorization.DemandRead(workspace, request);

            throw new RedirectException(CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>()?
                .BindParameters(new WorkspaceKeyParameter(workspace.Key)));
        }
    }
}
