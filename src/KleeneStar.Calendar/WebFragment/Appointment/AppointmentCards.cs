using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebIcon;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebCore.WebUri;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebFragment.Appointment
{
    /// <summary>
    /// The schedule card of the appointment page, above the description: when the appointment
    /// takes place (the next occurrence of a series and the rule in words), in which zone, where,
    /// whether it closes when it is over, and the links changing it.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class AppointmentScheduleCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentScheduleCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var appointment = CalendarRequest.Appointment(request);

            if (appointment is null)
            {
                return null;
            }

            var schedule = CalendarHub.AppointmentManager.GetSchedule(appointment.Id);
            var zone = CalendarRequest.Zone(request);
            var writable = AppointmentAuthorization.MayWrite(appointment, request);

            var section = new ControlSection("appointment-schedule-section")
            {
                Header = _ => "kleenestar.calendar:schedule.header",
                HeaderIcon = _ => new IconCalendarDays(),
                Layout = _ => TypeLayoutSection.Rule
            };

            if (schedule is null)
            {
                section.Add(Text("appointment-schedule-none", I18N.Translate(request, "kleenestar.calendar:schedule.none"), secondary: true));
            }
            else
            {
                var list = new ControlList("appointment-schedule-list");
                var now = DateTime.UtcNow;
                var next = AppointmentSchedule.Occurrences(schedule, now, DateTime.MaxValue.AddYears(-1), 1).FirstOrDefault();
                var first = new AppointmentOccurrence(schedule.ObjectId, schedule.Start, schedule.End, schedule.AllDay, schedule.Recurrence is not null);

                list.Add(Item("when", new IconClock(), I18N.Translate(request, "kleenestar.calendar:schedule.when"),
                    CalendarRequest.FormatSpan(first.Start, first.End, first.AllDay, request)));

                if (schedule.Recurrence is not null)
                {
                    list.Add(Item("series", new IconRepeat(), I18N.Translate(request, "kleenestar.calendar:schedule.series"),
                        RecurrenceText.Describe(schedule.Recurrence, request, AppointmentTime.Zone(schedule.TimeZone))));

                    list.Add(Item("next", new IconCalendarCheck(), I18N.Translate(request, "kleenestar.calendar:schedule.next"),
                        next is null
                            ? I18N.Translate(request, "kleenestar.calendar:schedule.next.none")
                            : CalendarRequest.FormatSpan(next.Start, next.End, next.AllDay, request)));

                    var removed = AppointmentSchedule.Exceptions(schedule).Count;

                    if (removed > 0)
                    {
                        list.Add(Item("exceptions", new IconCalendarMinus(), I18N.Translate(request, "kleenestar.calendar:schedule.exceptions"),
                            removed.ToString(CalendarRequest.Culture(request))));
                    }
                }

                var planned = AppointmentTime.Zone(schedule.TimeZone);

                if (!string.Equals(AppointmentForm.ZoneOption(planned), AppointmentForm.ZoneOption(zone), StringComparison.OrdinalIgnoreCase))
                {
                    list.Add(Item("zone", new IconClock(), I18N.Translate(request, "kleenestar.calendar:schedule.zone"),
                        $"{AppointmentForm.ZoneOption(planned)} · {CalendarRequest.FormatSpan(first.Start, first.End, first.AllDay, planned, CalendarRequest.Culture(request))}"));
                }

                if (!string.IsNullOrWhiteSpace(schedule.Location))
                {
                    list.Add(Item("location", new IconLocationDot(), I18N.Translate(request, "kleenestar.calendar:schedule.location"), schedule.Location));
                }

                if (schedule.OrganizerId is Guid organizerId && CoreHub.IdentityManager.GetIdentity(organizerId) is { } organizer)
                {
                    list.Add(Item("organizer", new IconUser(), I18N.Translate(request, "kleenestar.calendar:schedule.organizer"), organizer.Name));
                }

                list.Add(Item("autoclose", new IconCheck(), I18N.Translate(request, "kleenestar.calendar:schedule.autoclose"),
                    schedule.Closed is not null
                        ? I18N.Translate(request, "kleenestar.calendar:schedule.autoclose.closed", AppointmentTime.ToLocal(schedule.Closed.Value, zone).ToString("g", CalendarRequest.Culture(request)))
                        : I18N.Translate(request, schedule.AutoClose ? "kleenestar.calendar:schedule.autoclose.on" : "kleenestar.calendar:schedule.autoclose.off")));

                section.Add(list);
            }

            if (writable)
            {
                var actions = new ControlPanel("appointment-schedule-actions");

                actions.Add(new ControlButtonLink("appointment-schedule-edit")
                {
                    Text = _ => "kleenestar.calendar:appointment.edit.label",
                    Icon = _ => new IconCalendarCheck(),
                    BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary),
                    Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.Two, PropertySpacing.Space.None, PropertySpacing.Space.None),
                    PrimaryAction = _ => new ActionModal("modal-form",
                        CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Schedule>()?.BindParameters(new ObjectKeyParameter(appointment.Key)),
                        TypeModalSize.ExtraLarge)
                });

                if (schedule?.Recurrence is not null)
                {
                    actions.Add(new ControlButtonLink("appointment-schedule-exclude")
                    {
                        Text = _ => "kleenestar.calendar:exclude.label",
                        Icon = _ => new IconCalendarMinus(),
                        BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Secondary),
                        PrimaryAction = _ => new ActionModal("modal-form",
                            CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Exclude>()?.BindParameters(new ObjectKeyParameter(appointment.Key)),
                            TypeModalSize.Default)
                    });
                }

                section.Add(actions);
            }

            return section.Render(renderContext, visualTree);
        }

        /// <summary>
        /// Builds a line of the card.
        /// </summary>
        /// <param name="id">The id of the line.</param>
        /// <param name="icon">The icon.</param>
        /// <param name="label">The label, translated.</param>
        /// <param name="value">The value.</param>
        /// <returns>The line.</returns>
        internal static ControlListItem Item(string id, IIcon icon, string label, string value)
        {
            // the list draws the text of an item only, so the label leads it
            return new ControlListItem("appointment-" + id)
            {
                Icon = _ => icon,
                Text = _ => $"{label}: {value}"
            };
        }

        /// <summary>
        /// Builds a text.
        /// </summary>
        /// <param name="id">The id.</param>
        /// <param name="text">The text, translated.</param>
        /// <param name="secondary">Whether it is shown subdued.</param>
        /// <returns>The text.</returns>
        internal static ControlText Text(string id, string text, bool secondary = false)
        {
            return new ControlText(id)
            {
                Text = _ => text,
                Format = _ => TypeFormatText.Small,
                TextColor = _ => new PropertyColorText(secondary ? TypeColorText.Secondary : TypeColorText.Default)
            };
        }
    }

    /// <summary>
    /// The participants card of the appointment page: who is invited, in which role, and what
    /// they answered - accepted, with reservations (and which), declined (and why), or not yet.
    /// An invited reader answers from here.
    /// </summary>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Order(-2)]
    [Cache]
    public sealed class AppointmentParticipantsCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentParticipantsCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var appointment = CalendarRequest.Appointment(request);

            if (appointment is null)
            {
                return null;
            }

            var participants = CalendarHub.AppointmentManager.GetParticipants(appointment.Id);
            var identityId = CalendarRequest.Identity(request);
            var counts = participants.GroupBy(x => x.Response).ToDictionary(x => x.Key, x => x.Count());

            var section = new ControlSection("appointment-participants-section")
            {
                Header = _ => "kleenestar.calendar:participant.header",
                HeaderIcon = _ => new IconUsers(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = _ => participants.Count == 0
                    ? null
                    : $"{counts.GetValueOrDefault(AppointmentResponse.Accepted)}/{participants.Count}"
            };

            if (participants.Count == 0)
            {
                section.Add(AppointmentScheduleCardFragment.Text("appointment-participants-none", I18N.Translate(request, "kleenestar.calendar:participant.none"), secondary: true));
            }
            else
            {
                var list = new ControlList("appointment-participants-list");

                foreach (var participant in participants)
                {
                    var name = participant.IdentityId is Guid id
                        ? CoreHub.IdentityManager.GetIdentity(id)?.Name ?? participant.Name
                        : participant.Name ?? participant.Email;

                    var answer = I18N.Translate(request, "kleenestar.calendar:response." + participant.Response.ToString().ToLowerInvariant());
                    var role = participant.Role == AppointmentParticipantRole.Required
                        ? null
                        : I18N.Translate(request, "kleenestar.calendar:role." + participant.Role.ToString().ToLowerInvariant());
                    var guest = participant.IdentityId is null ? I18N.Translate(request, "kleenestar.calendar:participant.guest") : null;

                    var description = string.Join(" · ", new[] { answer, role, guest, participant.Comment }.Where(x => !string.IsNullOrWhiteSpace(x)));

                    list.Add(new ControlListItem("appointment-participant-" + participant.Id.ToString("N"))
                    {
                        Icon = _ => participant.Response switch
                        {
                            AppointmentResponse.Accepted => new IconCheck(),
                            AppointmentResponse.Tentative => new IconQuestion(),
                            AppointmentResponse.Declined => new IconXmark(),
                            _ => new IconClock()
                        },
                        Color = _ => new PropertyColorText(participant.Response switch
                        {
                            AppointmentResponse.Accepted => TypeColorText.Success,
                            AppointmentResponse.Tentative => TypeColorText.Warning,
                            AppointmentResponse.Declined => TypeColorText.Danger,
                            _ => TypeColorText.Secondary
                        }),
                        Text = _ => (participant.IdentityId == identityId ? $"{name} ({I18N.Translate(request, "kleenestar.calendar:participant.you")})" : name)
                            + (string.IsNullOrWhiteSpace(description) ? string.Empty : " – " + description)
                    });
                }

                section.Add(list);
            }

            if (AppointmentAuthorization.MayRespond(appointment, request))
            {
                var actions = new ControlPanel("appointment-participants-actions");

                foreach (var (response, icon, color) in new (string, IIcon, TypeColorButton)[]
                {
                    ("accepted", new IconCheck(), TypeColorButton.Success),
                    ("tentative", new IconQuestion(), TypeColorButton.Warning),
                    ("declined", new IconXmark(), TypeColorButton.Danger)
                })
                {
                    actions.Add(new ControlButtonLink("appointment-respond-" + response)
                    {
                        Text = _ => "kleenestar.calendar:respond." + response,
                        Icon = _ => icon,
                        BackgroundColor = _ => new PropertyColorButton(color),
                        Outline = _ => true,
                        Size = _ => TypeSizeButton.Small,
                        Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.None, PropertySpacing.Space.One, PropertySpacing.Space.Two, PropertySpacing.Space.None),
                        PrimaryAction = _ => new ActionModal("modal-form",
                            CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Respond>()?
                                .BindParameters(new ObjectKeyParameter(appointment.Key))
                                .Add(new UriQuery("response", response)),
                            TypeModalSize.Default)
                    });
                }

                section.Add(actions);
            }

            return section.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The resources card of the appointment page: the rooms, devices or vehicles the appointment
    /// books, each linked to its asset page.
    /// </summary>
    [Section<SectionPropertyPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class AppointmentResourcesCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentResourcesCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card, or nothing for an appointment that books nothing.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var appointment = CalendarRequest.Appointment(request);

            if (appointment is null)
            {
                return null;
            }

            var resources = CalendarHub.AppointmentManager.GetResources(appointment.Id)
                .Select(x => CoreHub.ObjectManager.GetObject(x))
                .Where(x => x is not null)
                .ToList();

            if (resources.Count == 0)
            {
                return null;
            }

            var section = new ControlSection("appointment-resources-section")
            {
                Header = _ => "kleenestar.calendar:resource.header",
                HeaderIcon = _ => new IconDoorOpen(),
                Layout = _ => TypeLayoutSection.Rule
            };

            var list = new ControlList("appointment-resources-list");

            foreach (var resource in resources)
            {
                var item = new ControlListItem("appointment-resource-" + resource.Id.ToString("N"))
                {
                    Icon = _ => new IconCube()
                };

                item.Add(new ControlLink("appointment-resource-link-" + resource.Id.ToString("N"))
                {
                    Text = _ => string.IsNullOrWhiteSpace(resource.Summary) ? resource.Key : $"{resource.Summary} ({resource.Key})",
                    Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUriFrozen(resource)
                });

                list.Add(item);
            }

            section.Add(list);

            return section.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The conflicts card of the appointment page: the coming occurrences at which a participant
    /// attends something else or a resource is booked for something else. Shown only when there
    /// is a conflict.
    /// </summary>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class AppointmentConflictsCardFragment : FragmentControlPanel
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentConflictsCardFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
        }

        /// <summary>
        /// Renders the card, or nothing without conflict.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The card, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var request = renderContext?.Request;
            var appointment = CalendarRequest.Appointment(request);
            var manager = CalendarHub.AppointmentManager;
            var schedule = appointment is null ? null : manager.GetSchedule(appointment.Id);

            if (schedule is null || (schedule.SeriesEnd is not null && schedule.SeriesEnd < DateTime.UtcNow))
            {
                return null;
            }

            var participants = manager.GetParticipants(appointment.Id)
                .Where(x => x.IdentityId is not null && x.Response != AppointmentResponse.Declined)
                .Select(x => x.IdentityId.Value)
                .ToList();

            var conflicts = manager.FindConflicts(new AppointmentConflictQuery
            {
                ObjectId = appointment.Id,
                Start = schedule.Start,
                End = schedule.End,
                AllDay = schedule.AllDay,
                TimeZone = schedule.TimeZone,
                Recurrence = schedule.Recurrence,
                Exceptions = schedule.Exceptions,
                IdentityIds = participants,
                ResourceIds = manager.GetResources(appointment.Id)
            })
                .Where(x => x.End > DateTime.UtcNow)
                .Take(20)
                .ToList();

            if (conflicts.Count == 0)
            {
                return null;
            }

            var section = new ControlSection("appointment-conflicts-section")
            {
                Header = _ => "kleenestar.calendar:conflict.title",
                HeaderIcon = _ => new IconTriangleExclamation(),
                Layout = _ => TypeLayoutSection.Rule,
                Badge = _ => conflicts.Count.ToString(CalendarRequest.Culture(request))
            };

            var list = new ControlList("appointment-conflicts-list");

            foreach (var (conflict, index) in conflicts.Select((x, i) => (x, i)))
            {
                var item = new ControlListItem("appointment-conflict-" + index)
                {
                    Icon = _ => conflict.Kind == AppointmentConflictKind.Resource ? new IconDoorOpen() : new IconUser(),
                    Color = _ => new PropertyColorText(conflict.Kind == AppointmentConflictKind.Resource ? TypeColorText.Danger : TypeColorText.Warning)
                };

                if (conflict.Other is not null)
                {
                    item.Add(new ControlLink("appointment-conflict-link-" + index)
                    {
                        Text = _ => AppointmentPlanning.Describe(conflict, request),
                        Uri = _ => CoreHub.ObjectKindManager.ResolveDetailUriFrozen(conflict.Other)
                    });
                }
                else
                {
                    item.Text = _ => AppointmentPlanning.Describe(conflict, request);
                }

                list.Add(item);
            }

            section.Add(list);

            return section.Render(renderContext, visualTree);
        }
    }

    /// <summary>
    /// The headline button of the appointment page opening the dialog that changes its time,
    /// series, participants and resources, for whoever may change it.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Index>]
    [Order(-1)]
    [Cache]
    public sealed class AppointmentScheduleButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public AppointmentScheduleButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.calendar:appointment.edit.label";
            Icon = _ => new IconCalendarCheck();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Appointment._objectkey_.Schedule>()?
                    .BindParameters(renderContext.Request),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Renders the button, or nothing for a caller who may not change the appointment.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var appointment = CalendarRequest.Appointment(renderContext?.Request);

            return AppointmentAuthorization.MayWrite(appointment, renderContext?.Request)
                ? base.Render(renderContext, visualTree)
                : null;
        }
    }
}
