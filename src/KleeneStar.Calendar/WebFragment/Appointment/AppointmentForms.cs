using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Model.Entities;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebFragment;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebFragment.Appointment
{
    /// <summary>
    /// The dialog planning a new appointment in a workspace
    /// (<see cref="global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Add"/>). Opened with
    /// <c>?template=</c>, it starts from what the template presets; the template travels as the
    /// form's item id, so its load and its submit both name it.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Add>]
    [Cache]
    public sealed class AppointmentAddFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentAddFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Appointment>();
            ItemId = renderContext => renderContext.Request.GetParameter("template")?.Value;
            Submit.Text = _ => "kleenestar.calendar:appointment.add.submit";
            Submit.Icon = _ => new IconCalendarPlus();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;
        }

        /// <summary>
        /// Renders the dialog with the inputs bound to the workspace.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without a workspace.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = CalendarRequest.Workspace(renderContext.Request);

            if (workspace is null || !CalendarRequest.Classes(workspace).Any(x => AppointmentAuthorization.MayCreate(x, renderContext.Request)))
            {
                return null;
            }

            return base.Render(renderContext, visualTree, AppointmentFormItems.Build(workspace, null));
        }
    }

    /// <summary>
    /// The dialog changing the time, the series, the participants and the resources of an
    /// appointment (<see cref="global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Schedule"/>).
    /// Its item id is the appointment's, so the detail page reloads once it saved
    /// (<c>kleenestar.core.object.refresh.js</c>).
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Schedule>]
    [Cache]
    public sealed class AppointmentEditFormFragment : FragmentControlDataFormEdit
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentEditFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Index>();
            ItemId = renderContext => CalendarRequest.Appointment(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.calendar:appointment.edit.submit";
            Submit.Icon = _ => new IconCalendarCheck();
        }

        /// <summary>
        /// Renders the dialog with the inputs bound to the appointment's workspace.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> without an appointment.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var appointment = CalendarRequest.Appointment(renderContext.Request);

            if (appointment is null)
            {
                return null;
            }

            var workspace = appointment.Workspace ?? CoreHub.WorkspaceManager.GetWorkspace(appointment.WorkspaceId);

            return base.Render(renderContext, visualTree, AppointmentFormItems.Build(workspace, appointment));
        }
    }

    /// <summary>
    /// The dialog in which an invited account answers
    /// (<see cref="global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Respond"/>): accepted,
    /// accepted with reservations, or declined, with the reservation or the reason.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Respond>]
    [Cache]
    public sealed class AppointmentRespondFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentRespondFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Respond>();

            // the answer the participant clicked on travels as the item id, so the dialog starts on it
            ItemId = renderContext => renderContext.Request.GetParameter("response")?.Value;
            Submit.Text = _ => "kleenestar.calendar:respond.submit";
            Submit.Icon = _ => new IconReply();
            SuccessAction = _ => TypeRestFormSuccess.FollowUri;

            Add
            (
                AppointmentFormItems.Static
                (
                    global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Respond.ResponseField,
                    "kleenestar.calendar:respond.response.label",
                    ["accepted", "tentative", "declined"],
                    "kleenestar.calendar:response."
                ),
                new ControlFormItemInputText
                {
                    Name = _ => global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Respond.CommentField,
                    Label = _ => "kleenestar.calendar:respond.comment.label",
                    Help = _ => "kleenestar.calendar:respond.comment.help",
                    Placeholder = _ => "kleenestar.calendar:respond.comment.placeholder",
                    Format = _ => TypeEditTextFormat.Multiline,
                    Rows = _ => 3,
                    MaxLength = _ => 1000
                }
            );
        }
    }

    /// <summary>
    /// The dialog removing one occurrence from a series
    /// (<see cref="global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Exclude"/>): the coming
    /// occurrences are offered, the next first.
    /// </summary>
    [Section<SectionContentPreferences>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Exclude>]
    [Cache]
    public sealed class AppointmentExcludeFormFragment : FragmentControlDataFormAdd
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentExcludeFormFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            this.DataService<global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Exclude>();
            ItemId = renderContext => CalendarRequest.Appointment(renderContext.Request)?.Id.ToString();
            Submit.Text = _ => "kleenestar.calendar:exclude.submit";
            Submit.Icon = _ => new IconCalendarMinus();
        }

        /// <summary>
        /// Renders the dialog with the coming occurrences.
        /// </summary>
        /// <param name="renderContext">The context in which the form is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The dialog, or <see langword="null"/> for an appointment that is no series.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var appointment = CalendarRequest.Appointment(renderContext.Request);
            var schedule = appointment is null ? null : CalendarHub.AppointmentManager.GetSchedule(appointment.Id);

            if (schedule?.Recurrence is null)
            {
                return null;
            }

            var selection = new ControlFormItemInputSelection
            {
                Name = _ => global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Exclude.OccurrenceField,
                Label = _ => "kleenestar.calendar:exclude.occurrence.label",
                Help = _ => "kleenestar.calendar:exclude.occurrence.help",
                StickySelection = _ => true,
                Required = _ => true
            };

            foreach (var (id, text) in global::KleeneStar.Calendar.WWW.Api._1_.Appointments._objectkey_.Exclude.Offered(schedule, renderContext.Request))
            {
                selection.Add(new ControlFormItemInputSelectionItem(id) { Text = _ => text });
            }

            return base.Render(renderContext, visualTree, [selection]);
        }
    }
}
