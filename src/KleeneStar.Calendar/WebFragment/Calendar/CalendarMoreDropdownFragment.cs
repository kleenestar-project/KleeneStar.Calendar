using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebFragment.Calendar
{
    /// <summary>
    /// The dropdown of the calendar headline: a new appointment from one of the templates of the
    /// workspace's appointment classes, including another calendar or a WQL query, importing an
    /// iCalendar file and exporting the calendar as one.
    /// </summary>
    /// <remarks>
    /// The entries depend on the workspace and on what the caller may do, so the dropdown is
    /// composed per render; a dropdown left without entries is not rendered.
    /// </remarks>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Order(1)]
    [Cache]
    public sealed class CalendarMoreDropdownFragment : FragmentControlDropdown
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarMoreDropdownFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the dropdown with the entries the caller may use.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The dropdown, or <see langword="null"/> when it has no entry.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null)
            {
                return null;
            }

            var dropdown = new ControlDropdown("calendar-more")
            {
                Text = _ => "kleenestar.calendar:calendar.more.label",
                Icon = _ => new IconCalendarDays(),
                Color = _ => new PropertyColorButton(TypeColorButton.Secondary),
                Outline = _ => true,
                AlignmentMenu = _ => TypeAlignmentDropdownMenu.Right
            };

            var bound = new WorkspaceKeyParameter(workspace.Key);
            var any = false;

            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request)).ToList();
            var templates = CalendarRequest.Templates(classes)
                .Select(x => (Id: x.Id.ToString(), x.Name))
                .ToList();

            if (templates.Count > 0)
            {
                dropdown.AddHeader("kleenestar.calendar:template.header");

                foreach (var (templateId, name) in templates.DistinctBy(x => x.Id).OrderBy(x => x.Name))
                {
                    dropdown.Add(new ControlDropdownItemLink("calendar-template-" + templateId)
                    {
                        Text = _ => name,
                        Icon = _ => new IconCalendarPlus(),
                        PrimaryAction = _ => new ActionModal
                        (
                            "modal-form",
                            CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Add>()?
                                .BindParameters(bound)
                                .Add(new UriQuery("template", templateId)),
                            TypeModalSize.ExtraLarge
                        )
                    });
                }

                dropdown.AddSeparator();
                any = true;
            }

            if (AppointmentAuthorization.MayArrange(workspace, request))
            {
                dropdown.Add(new ControlDropdownItemLink("calendar-include")
                {
                    Text = _ => "kleenestar.calendar:source.add.label",
                    Icon = _ => new IconLayerGroup(),
                    PrimaryAction = _ => new ActionModal
                    (
                        "modal-form",
                        CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Sources>()?.BindParameters(bound),
                        TypeModalSize.Large
                    )
                });

                any = true;
            }

            if (classes.Count > 0)
            {
                dropdown.Add(new ControlDropdownItemLink("calendar-import")
                {
                    Text = _ => "kleenestar.calendar:import.label",
                    Icon = _ => new IconFileImport(),
                    PrimaryAction = _ => new ActionModal
                    (
                        "modal-form",
                        CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Import>()?.BindParameters(bound),
                        TypeModalSize.Large
                    )
                });

                any = true;
            }

            if (AppointmentAuthorization.MayRead(workspace, request))
            {
                dropdown.Add(new ControlDropdownItemLink("calendar-export")
                {
                    Text = _ => "kleenestar.calendar:export.label",
                    Icon = _ => new IconFileExport(),
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Export>()?.BindParameters(bound)
                });

                dropdown.Add(new ControlDropdownItemLink("calendar-export-layers")
                {
                    Text = _ => "kleenestar.calendar:export.layers.label",
                    Icon = _ => new IconFileExport(),
                    Uri = _ => CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Export>()?
                        .BindParameters(bound)
                        .Add(new UriQuery("layers", "true"))
                });

                any = true;
            }

            return any ? dropdown.Render(renderContext, visualTree) : null;
        }
    }
}
