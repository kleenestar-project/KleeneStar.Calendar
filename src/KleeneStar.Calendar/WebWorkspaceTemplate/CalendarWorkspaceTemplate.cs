using KleeneStar.Core.WebWorkspaceTemplate;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WebWorkspaceTemplate
{
    /// <summary>
    /// The workspace template of a team calendar: one appointment class, <c>Meeting</c>, whose
    /// workflow knows the states an appointment passes - planned, confirmed, held, cancelled -
    /// so that the calendar can close what is over into <c>Held</c>.
    /// </summary>
    /// <remarks>
    /// An existing workspace gets a calendar by a class of the appointment kind; the template is
    /// the shortcut for a workspace that is mainly one. The texts are internationalization keys,
    /// resolved once in the language of whoever creates the workspace.
    /// </remarks>
    public sealed class CalendarWorkspaceTemplate : IWorkspaceTemplate
    {
        /// <summary>
        /// The address of the icon of the template and its class.
        /// </summary>
        public const string IconPath = "/kleenestar/kleenestar.calendar/assets/img/kleenestar.calendar.svg";

        /// <summary>
        /// Gets the stable key of the template.
        /// </summary>
        public string Key => "kleenestar.calendar.teamcalendar";

        /// <summary>
        /// Gets the internationalization key of the display name.
        /// </summary>
        public string Name => "kleenestar.calendar:template.teamcalendar.name";

        /// <summary>
        /// Gets the internationalization key of the description.
        /// </summary>
        public string Description => "kleenestar.calendar:template.teamcalendar.description";

        /// <summary>
        /// Gets the icon of the template.
        /// </summary>
        public IIcon Icon => ImageIcon.FromString(IconPath);

        /// <summary>
        /// Gets the suggested key of the workspace.
        /// </summary>
        public string SuggestedKey => "CAL";

        /// <summary>
        /// Gets the categories the workspace is filed under.
        /// </summary>
        public IEnumerable<string> Categories => [];

        /// <summary>
        /// Gets the display order among the templates offered.
        /// </summary>
        public int Order => 150;

        /// <summary>
        /// Gets the classes the workspace starts with.
        /// </summary>
        public IEnumerable<WorkspaceTemplateClass> Classes =>
        [
            new WorkspaceTemplateClass
            {
                Name = "Meeting",
                Description = "kleenestar.calendar:template.teamcalendar.class.meeting",
                Icon = IconPath,
                Kind = ObjectKind.Appointment,
                Fields =
                [
                    new WorkspaceTemplateField
                    {
                        Name = "Description",
                        Description = "kleenestar.calendar:template.field.description",
                        Type = FieldType.Text
                    },
                    new WorkspaceTemplateField
                    {
                        Name = "Status",
                        Description = "kleenestar.calendar:template.field.status",
                        Type = FieldType.Workflow
                    },
                    new WorkspaceTemplateField
                    {
                        Name = "Labels",
                        Description = "kleenestar.calendar:template.field.labels",
                        Type = FieldType.Tag,
                        Tab = WorkspaceTemplateField.DetailsTab
                    }
                ],
                Workflow = Workflow()
            }
        ];

        /// <summary>
        /// Declares the workflow of an appointment.
        /// </summary>
        /// <returns>The workflow.</returns>
        public static WorkspaceTemplateWorkflow Workflow()
        {
            const string planned = "kleenestar.calendar:template.status.planned";
            const string confirmed = "kleenestar.calendar:template.status.confirmed";
            const string held = "kleenestar.calendar:template.status.held";
            const string cancelled = "kleenestar.calendar:template.status.cancelled";

            return new WorkspaceTemplateWorkflow
            {
                Name = "kleenestar.calendar:template.workflow.name",
                Description = "kleenestar.calendar:template.workflow.description",
                Statuses =
                [
                    new WorkspaceTemplateStatus { Name = planned, Description = "kleenestar.calendar:template.status.planned.description", Category = WorkspaceTemplateStatus.ToDo, Icon = "/kleenestar/assets/icons/state-new.svg" },
                    new WorkspaceTemplateStatus { Name = confirmed, Description = "kleenestar.calendar:template.status.confirmed.description", Category = WorkspaceTemplateStatus.InProgress, Icon = "/kleenestar/assets/icons/state-progress.svg" },

                    // the closing state reached by the automatic closing comes first among the
                    // closing ones, so the calendar closes into it
                    new WorkspaceTemplateStatus { Name = held, Description = "kleenestar.calendar:template.status.held.description", Category = WorkspaceTemplateStatus.Done, IsEnd = true, Icon = "/kleenestar/assets/icons/state-resolved.svg" },
                    new WorkspaceTemplateStatus { Name = cancelled, Description = "kleenestar.calendar:template.status.cancelled.description", Category = WorkspaceTemplateStatus.Done, IsEnd = true, Icon = "/kleenestar/assets/icons/state-closed.svg" }
                ],
                Transitions =
                [
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.confirm", From = planned, To = confirmed },
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.hold", From = planned, To = held },
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.hold", From = confirmed, To = held },
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.cancel", From = planned, To = cancelled },
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.cancel", From = confirmed, To = cancelled },
                    new WorkspaceTemplateTransition { Name = "kleenestar.calendar:template.transition.reschedule", From = cancelled, To = planned }
                ]
            };
        }
    }
}
