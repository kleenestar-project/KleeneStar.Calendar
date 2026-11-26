using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebScope;
using KleeneStar.Core.WebUri;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WWW.Calendar._workspacekey_
{
    /// <summary>
    /// The calendar of a workspace: its appointments and the layers it includes (other
    /// workspaces, WQL queries) on a month, week or agenda grid. The sidebar carries the kind
    /// links shared with the core's kind overviews (the page is an
    /// <see cref="IScopeObjectKindOverview"/>) and the coming appointments.
    /// </summary>
    [WebIcon<IconCalendarDays>]
    [Title("kleenestar.calendar:kind.label")]
    [WorkspaceKeySegment]
    [Scope<IScopeGeneral>]
    [Scope<IScopeObjectKindOverview>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral, IScopeObjectKindOverview
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;
            var workspace = CalendarRequest.Workspace(request);

            AppointmentAuthorization.DemandRead(workspace, request);

            visualTree.BreadcrumbUri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = workspace?.Key,
                    Uri = CoreHub.GetUri<Index>()
                        .BindParameters(new WorkspaceKeyParameter(workspace?.Key))
                })
                .ToUri()
                .BindParameters(request);

            visualTree.Title = workspace?.Name;
            visualTree.Content.MainPanel.Headline.Title = I18N.Translate(request, "kleenestar.calendar:kind.label");

            // the calendar is workspace content, so it advances the workspace's ranking like the
            // core's kind overviews
            if (workspace is not null)
            {
                CoreHub.WorkspaceManager.RecordVisit(CalendarRequest.Identity(request), workspace.Id);
            }
        }
    }
}
