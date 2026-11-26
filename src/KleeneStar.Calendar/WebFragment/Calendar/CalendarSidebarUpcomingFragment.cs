using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebPolicies;
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
    /// The "coming up" section of the calendar sidebar, below the kind links: the next
    /// appointments of the calendar (its own and those of its layers) within the coming days -
    /// by default the next 10 within 7 days (<see cref="AppointmentSettings.UpcomingCount"/>,
    /// <see cref="AppointmentSettings.UpcomingDays"/>). Closed and cancelled appointments are
    /// left out; each entry shows the time and the title and links to the object.
    /// </summary>
    [Section<SectionSidebarPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Condition<global::KleeneStar.Core.WebPermission.PolicyCondition<WorkspaceViewPolicy>>]
    [Order(10)]
    [Cache]
    public sealed class CalendarSidebarUpcomingFragment : FragmentControlSidebarItemLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarSidebarUpcomingFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the header followed by one entry per coming appointment, or a disabled entry
        /// saying there is none.
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

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return null;
            }

            var settings = CalendarHub.Settings;
            var upcoming = CalendarHub.AppointmentManager.GetUpcoming(workspace, DateTime.UtcNow, TimeSpan.FromDays(settings.UpcomingDays), settings.UpcomingCount);
            var currentKey = CalendarRequest.AnyObject(request)?.Key;

            var header = new ControlSidebarItemHeader(Id + "-header")
            {
                Text = _ => "kleenestar.calendar:upcoming.label"
            };

            var nodes = new HtmlList(header.Render(renderContext, visualTree));

            if (upcoming.Count == 0)
            {
                var empty = new ControlSidebarItemLink("calendar-upcoming-empty")
                {
                    Text = _ => "kleenestar.calendar:upcoming.none",
                    Active = _ => TypeActive.Disabled
                };

                nodes.Add(empty.Render(renderContext, visualTree));

                return nodes;
            }

            var zone = CalendarRequest.Zone(request);
            var culture = CalendarRequest.Culture(request);

            foreach (var entry in upcoming)
            {
                var entity = entry.Object;
                var when = CalendarRequest.FormatSpan(entry.Start, entry.End, entry.AllDay, zone, culture);
                var title = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : entity.Summary;

                var link = new ControlSidebarItemLink("calendar-upcoming-" + entry.Id.Replace("@", "-"))
                {
                    Text = _ => title,
                    Tooltip = _ => when + (string.IsNullOrWhiteSpace(entry.Location) ? string.Empty : " · " + entry.Location),
                    // frozen so the sidebar's request re-bind cannot repoint every entry at the
                    // object the page shows
                    Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUriFrozen(entity),
                    Icon = _ => entry.IsAppointment ? new IconCalendarDays() : ObjectIcon.Resolve(entity, new IconCalendarDays()),
                    Badge = _ => AppointmentTime.ToLocal(entry.Start, zone).ToString(entry.AllDay ? "ddd" : "ddd HH:mm", culture),
                    BadgeColor = _ => new PropertyColorBackgroundBadge(TypeColorBackgroundBadge.Secondary),
                    Active = _ => string.Equals(entity.Key, currentKey, StringComparison.OrdinalIgnoreCase)
                        ? TypeActive.Active
                        : TypeActive.None
                };

                nodes.Add(link.Render(renderContext, visualTree));
            }

            return nodes;
        }
    }
}
