using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebParameter;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebPolicies;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebFragment.Calendar
{
    /// <summary>
    /// The "calendars" section of the calendar sidebar: the workspace's own calendar and the
    /// layers it includes - other workspaces and WQL queries - each in the colour its entries
    /// carry. Whoever may arrange the layers finds an entry to include another one, and each
    /// layer opens the dialog removing it.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(20)]
    [Cache]
    public sealed class CalendarSidebarLayersFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarSidebarLayersFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the header, the own calendar, the layers and the entry including another.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The entries, or <see langword="null"/> when the fragment is suppressed.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;

            if (!FragmentContext.Conditions.Check(request))
            {
                return null;
            }

            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null)
            {
                return null;
            }

            var arrange = AppointmentAuthorization.MayArrange(workspace, request);

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.calendar:source.label"
            };

            var own = new ControlSidebarItemLink("calendar-layer-own")
            {
                Text = _ => workspace.Name,
                Icon = _ => new IconCalendarDays(),
                Tooltip = _ => "kleenestar.calendar:source.own"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree), own.Render(renderContext, visualTree));

            foreach (var source in CalendarHub.AppointmentManager.GetSources(workspace.Id))
            {
                var description = Describe(source);

                var link = new ControlSidebarItemLink("calendar-layer-" + source.Id.ToString("N"))
                {
                    Text = _ => source.Name,
                    Tooltip = _ => description,
                    Icon = _ => string.Equals(source.Kind, AppointmentCalendarSource.QueryKind, StringComparison.OrdinalIgnoreCase)
                        ? new IconLayerGroup()
                        : new IconCalendarPlus(),
                    Badge = _ => "●",
                    BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Light),
                    Active = _ => source.Enabled ? TypeActive.None : TypeActive.Disabled
                };

                if (arrange)
                {
                    link.PrimaryAction = _ => new ActionModal
                    (
                        "modal-form",
                        CoreHub.GetUri<global::KleeneStar.Calendar.WWW.CalendarSource._sourceid_.Delete>()?
                            .BindParameters(new SourceIdParameter(source.Id)),
                        TypeModalSize.Default
                    );
                }

                nodes.Add(link.Render(renderContext, visualTree));
            }

            if (arrange)
            {
                var add = new ControlSidebarItemLink("calendar-layer-add")
                {
                    Text = _ => "kleenestar.calendar:source.add.label",
                    Icon = _ => new IconPlus(),
                    PrimaryAction = _ => new ActionModal
                    (
                        "modal-form",
                        CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Sources>()?
                            .BindParameters(new WorkspaceKeyParameter(workspace.Key)),
                        TypeModalSize.Large
                    )
                };

                nodes.Add(add.Render(renderContext, visualTree));
            }

            return nodes;
        }

        /// <summary>
        /// Describes what a layer shows.
        /// </summary>
        /// <param name="source">The layer.</param>
        /// <returns>The text.</returns>
        private static string Describe(AppointmentCalendarSource source)
        {
            if (string.Equals(source.Kind, AppointmentCalendarSource.QueryKind, StringComparison.OrdinalIgnoreCase))
            {
                return "WQL: " + source.Query;
            }

            var workspace = source.SourceWorkspaceId is Guid id ? CoreHub.WorkspaceManager.GetWorkspace(id) : null;

            return workspace is null ? source.Name : $"{workspace.Name} ({workspace.Key})";
        }
    }
}
