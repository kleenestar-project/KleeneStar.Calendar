using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebFragment.Appointment;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;
using SourcesApi = global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Sources;
using ImportApi = global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Import;

namespace KleeneStar.Calendar.WebFragment.Calendar
{
    /// <summary>
    /// The dialog including a layer in the calendar of a workspace
    /// (<see cref="global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Sources"/>): another
    /// workspace, whose appointments are shown, or a WQL query, whose objects are shown - an
    /// appointment by its schedule, another object by the date fields of its class.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Sources>]
    [Cache]
    public sealed class CalendarSourceAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarSourceAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<SourcesApi>();
            Submit.Text = _ => "kleenestar.calendar:source.add.submit";
            Submit.Icon = _ => new IconLayerGroup();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with the workspace selection bound to the workspace.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a workspace.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = CalendarRequest.Workspace(renderContext.Request);

            if (workspace is null)
            {
                return null;
            }

            var kind = AppointmentFormItems.Static
            (
                SourcesApi.KindField,
                "kleenestar.calendar:source.kind.label",
                [AppointmentCalendarSource.WorkspaceKind, AppointmentCalendarSource.QueryKind],
                "kleenestar.calendar:source.kind."
            );

            var color = new ControlFormItemInputSelection
            {
                Name = _ => SourcesApi.ColorField,
                Label = _ => "kleenestar.calendar:source.color.label",
                StickySelection = _ => true
            };

            foreach (var value in SourcesApi.Colors)
            {
                color.Add(new ControlFormItemInputSelectionItem(value) { Text = _ => "●  " + value });
            }

            return base.Render(renderContext, visualTree,
            [
                new ControlFormItemInputText
                {
                    Name = _ => SourcesApi.NameField,
                    Label = _ => "kleenestar.calendar:source.name.label",
                    Placeholder = _ => "kleenestar.calendar:source.name.placeholder"
                },
                new ControlFormItemGroupColumnHorizontal(kind, color),
                new ControlDataFormItemInputSelection
                {
                    Name = _ => SourcesApi.WorkspaceField,
                    Label = _ => "kleenestar.calendar:source.workspace.label",
                    Help = _ => "kleenestar.calendar:source.workspace.help",
                    ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Workspaces>()?
                        .BindParameters(new WorkspaceKeyParameter(workspace.Key))?
                        .ToString())
                },
                new ControlFormItemInputText
                {
                    Name = _ => SourcesApi.QueryField,
                    Label = _ => "kleenestar.calendar:source.query.label",
                    Help = _ => "kleenestar.calendar:source.query.help",
                    Placeholder = _ => "kleenestar.calendar:source.query.placeholder",
                    Format = _ => TypeEditTextFormat.Multiline,
                    Rows = _ => 3
                }
            ]);
        }
    }

    /// <summary>
    /// The dialog removing a layer from a workspace calendar
    /// (<see cref="global::KleeneStar.Calendar.WWW.CalendarSource._sourceid_.Delete"/>). The
    /// endpoint names the layer in the prolog of the dialog.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.CalendarSource._sourceid_.Delete>]
    [Cache]
    public sealed class CalendarSourceDeleteFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarSourceDeleteFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Calendar.WWW.Api._1_.CalendarSources._sourceid_.Index>();
            Submit.Text = _ => "kleenestar.calendar:source.delete.submit";
            Submit.Icon = _ => new IconTrash();
            Submit.Color = _ => new PropertyColorButton(TypeColorButton.Danger);
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;

            Add(new ControlFormItemStaticText("calendar-source-delete-text")
            {
                Text = _ => "kleenestar.calendar:source.delete.description"
            });
        }
    }

    /// <summary>
    /// The dialog importing the events of an iCalendar file into the calendar of a workspace
    /// (<see cref="global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Import"/>). A form posts
    /// no file, so the chosen file is read in the browser into the content input
    /// (<c>kleenestar.calendar.import.js</c>), which can also take pasted text.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Import>]
    [Cache]
    public sealed class CalendarImportFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarImportFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<ImportApi>();
            Submit.Text = _ => "kleenestar.calendar:import.submit";
            Submit.Icon = _ => new IconFileImport();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with the class selection bound to the workspace.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a class to import into.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = CalendarRequest.Workspace(renderContext.Request);

            if (workspace is null || !CalendarRequest.Classes(workspace).Any(x => AppointmentAuthorization.MayCreate(x, renderContext.Request)))
            {
                return null;
            }

            var file = new ControlFormItemInputFile
            {
                Name = _ => ImportApi.FileField,
                Label = _ => "kleenestar.calendar:import.file.label",
                Help = _ => "kleenestar.calendar:import.file.help"
            };

            file.AddAcceptFile(".ics", "text/calendar");

            return base.Render(renderContext, visualTree,
            [
                new ControlDataFormItemInputSelection
                {
                    Name = _ => ImportApi.ClassField,
                    Label = _ => "kleenestar.calendar:appointment.class.label",
                    Help = _ => "kleenestar.calendar:import.class.help",
                    StickySelection = _ => true,
                    ServiceFactory = _ => DataServiceDescriptor.QueryData(CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Classes>()?
                        .BindParameters(new WorkspaceKeyParameter(workspace.Key))?
                        .ToString())
                },
                file,
                new ControlFormItemInputText
                {
                    Name = _ => ImportApi.ContentField,
                    Label = _ => "kleenestar.calendar:import.content.label",
                    Help = _ => "kleenestar.calendar:import.content.help",
                    Placeholder = _ => "BEGIN:VCALENDAR …",
                    Format = _ => TypeEditTextFormat.Multiline,
                    Rows = _ => 6
                }
            ]);
        }
    }
}
