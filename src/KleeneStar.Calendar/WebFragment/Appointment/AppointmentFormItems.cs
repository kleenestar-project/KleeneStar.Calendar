using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebControl;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebApiControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WebFragment.Appointment
{
    /// <summary>
    /// Builds the inputs of the appointment dialog, shared by the dialog planning a new
    /// appointment and the one changing an appointment: the time and place, the series, the
    /// participants and the resources, each on a tab, and the conflict panel below them.
    /// </summary>
    /// <remarks>
    /// The selections of classes, templates, accounts and resources are fed by the endpoints of
    /// the calendar of the workspace the appointment belongs to, so they are bound per render.
    /// </remarks>
    public static class AppointmentFormItems
    {
        /// <summary>
        /// Builds the inputs.
        /// </summary>
        /// <param name="workspace">The workspace the appointment belongs to.</param>
        /// <param name="appointment">The appointment being changed, or <see langword="null"/> for a new one.</param>
        /// <returns>The inputs.</returns>
        public static IEnumerable<ControlFormItem> Build(Workspace workspace, Model.Entities.Object appointment)
        {
            var bound = new WorkspaceKeyParameter(workspace?.Key);

            string Service<T>() where T : WebExpress.WebCore.WebEndpoint.IEndpoint
            {
                return CoreHub.GetUri<T>()?.BindParameters(bound)?.ToString();
            }

            var general = new ControlFormItemGroupTabView("appointment-tab-general")
            {
                Title = _ => "kleenestar.calendar:appointment.tab.general",
                Icon = _ => new IconCalendarDays()
            };

            general.Add(new ControlFormItemInputText
            {
                Name = _ => AppointmentForm.Summary,
                Label = _ => "kleenestar.calendar:appointment.summary.label",
                Placeholder = _ => "kleenestar.calendar:appointment.summary.placeholder",
                Required = _ => true
            });

            if (appointment is null)
            {
                general.Add(new ControlFormItemGroupColumnHorizontal
                (
                    new ControlDataFormItemInputSelection
                    {
                        Name = _ => AppointmentForm.ClassId,
                        Label = _ => "kleenestar.calendar:appointment.class.label",
                        Help = _ => "kleenestar.calendar:appointment.class.help",
                        StickySelection = _ => true,
                        ServiceFactory = _ => DataServiceDescriptor.QueryData(Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Classes>())
                    },
                    new ControlDataFormItemInputSelection
                    {
                        Name = _ => AppointmentForm.TemplateId,
                        Label = _ => "kleenestar.calendar:template.label",
                        Help = _ => "kleenestar.calendar:template.help",
                        Placeholder = _ => "kleenestar.calendar:template.none",
                        ServiceFactory = _ => DataServiceDescriptor.QueryData(Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Templates>())
                    }
                ));
            }

            general.Add(new ControlFormItemGroupColumnHorizontal
            (
                new ControlFormItemInputDate
                {
                    Name = _ => AppointmentForm.StartDate,
                    Label = _ => "kleenestar.calendar:appointment.start.label",
                    Required = _ => true
                },
                Time(AppointmentForm.StartTime, "kleenestar.calendar:appointment.starttime.label"),
                new ControlFormItemInputDate
                {
                    Name = _ => AppointmentForm.EndDate,
                    Label = _ => "kleenestar.calendar:appointment.end.label"
                },
                Time(AppointmentForm.EndTime, "kleenestar.calendar:appointment.endtime.label")
            ));

            general.Add(new ControlFormItemGroupColumnHorizontal
            (
                new ControlFormItemInputCheck
                {
                    Name = _ => AppointmentForm.AllDay,
                    Label = _ => "kleenestar.calendar:appointment.allday.label",
                    Help = _ => "kleenestar.calendar:appointment.allday.help",
                    Layout = _ => TypeLayoutCheck.Switch
                },
                Zones(),
                new ControlFormItemInputCheck
                {
                    Name = _ => AppointmentForm.AutoClose,
                    Label = _ => "kleenestar.calendar:appointment.autoclose.label",
                    Help = _ => "kleenestar.calendar:appointment.autoclose.help",
                    Layout = _ => TypeLayoutCheck.Switch
                }
            ));

            general.Add(new ControlFormItemInputText
            {
                Name = _ => AppointmentForm.Location,
                Label = _ => "kleenestar.calendar:appointment.location.label",
                Placeholder = _ => "kleenestar.calendar:appointment.location.placeholder",
                Icon = _ => new IconLocationDot()
            });

            general.Add(new ControlFormItemInputText
            {
                Name = _ => AppointmentForm.Description,
                Label = _ => "kleenestar.calendar:appointment.description.label",
                Format = _ => TypeEditTextFormat.Wysiwyg,
                LinkLibraryUri = CoreHub.EditorLinkLibrary
            });

            var series = new ControlFormItemGroupTabView("appointment-tab-series")
            {
                Title = _ => "kleenestar.calendar:appointment.tab.series",
                Icon = _ => new IconRepeat()
            };

            series.Add(new ControlFormItemGroupColumnHorizontal
            (
                Static(AppointmentForm.Repeat, "kleenestar.calendar:appointment.repeat.label", AppointmentForm.Frequencies, "kleenestar.calendar:appointment.repeat."),
                new ControlFormItemInputText
                {
                    Name = _ => AppointmentForm.Interval,
                    Label = _ => "kleenestar.calendar:appointment.interval.label",
                    Help = _ => "kleenestar.calendar:appointment.interval.help",
                    Pattern = _ => "[0-9]*"
                }
            ));

            var days = Static(AppointmentForm.Weekdays, "kleenestar.calendar:appointment.weekdays.label", AppointmentForm.Days, "kleenestar.calendar:weekday.");
            days.MultiSelect = _ => true;
            days.Help = _ => "kleenestar.calendar:appointment.weekdays.help";
            series.Add(days);

            series.Add(new ControlFormItemGroupColumnHorizontal
            (
                new ControlFormItemInputDate
                {
                    Name = _ => AppointmentForm.Until,
                    Label = _ => "kleenestar.calendar:appointment.until.label",
                    Help = _ => "kleenestar.calendar:appointment.until.help"
                },
                new ControlFormItemInputText
                {
                    Name = _ => AppointmentForm.Count,
                    Label = _ => "kleenestar.calendar:appointment.count.label",
                    Help = _ => "kleenestar.calendar:appointment.count.help",
                    Pattern = _ => "[0-9]*"
                }
            ));

            series.Add(new ControlFormItemInputText
            {
                Name = _ => AppointmentForm.Rule,
                Label = _ => "kleenestar.calendar:appointment.rule.label",
                Help = _ => "kleenestar.calendar:appointment.rule.help",
                Placeholder = _ => "FREQ=MONTHLY;BYDAY=2TU"
            });

            var people = new ControlFormItemGroupTabView("appointment-tab-participants")
            {
                Title = _ => "kleenestar.calendar:appointment.tab.participants",
                Icon = _ => new IconUsers()
            };

            people.Add(new ControlDataFormItemInputSelection
            {
                Name = _ => AppointmentForm.Participants,
                Label = _ => "kleenestar.calendar:participant.label",
                Help = _ => "kleenestar.calendar:participant.help",
                Placeholder = _ => "kleenestar.calendar:participant.placeholder",
                MultiSelect = _ => true,
                ServiceFactory = _ => DataServiceDescriptor.QueryData(Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Participants>())
            });

            people.Add(new ControlFormItemInputText
            {
                Name = _ => AppointmentForm.Guests,
                Label = _ => "kleenestar.calendar:guest.label",
                Help = _ => "kleenestar.calendar:guest.help",
                Placeholder = _ => "Alex Smith <alex@example.com>; Kim Lee",
                Format = _ => TypeEditTextFormat.Multiline,
                Rows = _ => 2
            });

            var resources = new ControlFormItemGroupTabView("appointment-tab-resources")
            {
                Title = _ => "kleenestar.calendar:appointment.tab.resources",
                Icon = _ => new IconDoorOpen()
            };

            resources.Add(new ControlDataFormItemInputSelection
            {
                Name = _ => AppointmentForm.Resources,
                Label = _ => "kleenestar.calendar:resource.label",
                Help = _ => "kleenestar.calendar:resource.help",
                Placeholder = _ => "kleenestar.calendar:resource.placeholder",
                MultiSelect = _ => true,
                ServiceFactory = _ => DataServiceDescriptor.QueryData(Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Resources>())
            });

            resources.Add(new ControlFormItemInputCheck
            {
                Name = _ => AppointmentForm.IgnoreConflicts,
                Label = _ => "kleenestar.calendar:conflict.ignore.label",
                Help = _ => "kleenestar.calendar:conflict.ignore.help",
                Layout = _ => TypeLayoutCheck.Switch
            });

            var tabs = new ControlFormItemGroupTab();
            tabs.AddView(general, series, people, resources);

            return
            [
                tabs,
                new ControlFormItemConflicts("appointment-conflicts")
                {
                    Uri = Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Conflicts>(),
                    Appointment = appointment?.Key
                }
            ];
        }

        /// <summary>
        /// Builds a time-of-day input.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <param name="label">The internationalization key of the label.</param>
        /// <returns>The input.</returns>
        private static ControlFormItemInputText Time(string name, string label)
        {
            return new ControlFormItemInputText
            {
                Name = _ => name,
                Label = _ => label,
                Placeholder = _ => "09:00",
                Pattern = _ => AppointmentForm.TimePattern,
                Icon = _ => new IconClock()
            };
        }

        /// <summary>
        /// Builds the zone selection: the common zones, the zone of the host first.
        /// </summary>
        /// <returns>The selection.</returns>
        private static ControlFormItemInputSelection Zones()
        {
            var selection = new ControlFormItemInputSelection
            {
                Name = _ => AppointmentForm.TimeZone,
                Label = _ => "kleenestar.calendar:appointment.timezone.label",
                Help = _ => "kleenestar.calendar:appointment.timezone.help",
                StickySelection = _ => true
            };

            var zones = new List<string>();
            var host = AppointmentForm.ZoneOption(AppointmentTime.Zone(CalendarHub.Settings.TimeZone));

            if (!string.IsNullOrWhiteSpace(host))
            {
                zones.Add(host);
            }

            zones.AddRange(AppointmentForm.Zones);

            foreach (var zone in zones.Distinct())
            {
                selection.Add(new ControlFormItemInputSelectionItem(zone) { Text = _ => zone });
            }

            return selection;
        }

        /// <summary>
        /// Builds a selection of fixed options whose texts are internationalization keys.
        /// </summary>
        /// <param name="name">The input name.</param>
        /// <param name="label">The internationalization key of the label.</param>
        /// <param name="options">The option ids.</param>
        /// <param name="prefix">The prefix of the option texts' keys.</param>
        /// <returns>The selection.</returns>
        public static ControlFormItemInputSelection Static(string name, string label, IEnumerable<string> options, string prefix)
        {
            var selection = new ControlFormItemInputSelection
            {
                Name = _ => name,
                Label = _ => label,
                StickySelection = _ => true
            };

            foreach (var option in options)
            {
                selection.Add(new ControlFormItemInputSelectionItem(option) { Text = _ => prefix + option.ToLowerInvariant() });
            }

            return selection;
        }
    }
}
