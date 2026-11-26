using KleeneStar.Core;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebMessage;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// Plans an appointment from a submitted dialog: checks it, shows what it collides with, and
    /// writes the object, its schedule, its participants and its bookings.
    /// </summary>
    /// <remarks>
    /// A participant who already attends something at the time is told about, not refused - a
    /// person decides whether to come. A resource booked twice is refused, because a room cannot
    /// be in two meetings, unless the dialog books it anyway on purpose.
    /// </remarks>
    public static class AppointmentPlanning
    {
        /// <summary>
        /// Returns the conflicts of a draft.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <param name="objectId">The appointment being changed, or <see langword="null"/>.</param>
        /// <returns>The conflicts.</returns>
        public static IReadOnlyList<AppointmentConflict> Conflicts(AppointmentDraft draft, Guid? objectId)
        {
            return CalendarHub.AppointmentManager.FindConflicts(new AppointmentConflictQuery
            {
                ObjectId = objectId,
                Start = draft.Start,
                End = draft.End,
                AllDay = draft.AllDay,
                TimeZone = draft.TimeZone,
                Recurrence = draft.Recurrence,
                Exceptions = objectId is Guid id ? CalendarHub.AppointmentManager.GetSchedule(id)?.Exceptions : null,
                IdentityIds = draft.IdentityIds,
                ResourceIds = draft.ResourceIds
            });
        }

        /// <summary>
        /// Describes a conflict in the language of a request.
        /// </summary>
        /// <param name="conflict">The conflict.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        public static string Describe(AppointmentConflict conflict, IRequest request)
        {
            var when = CalendarRequest.FormatSpan(conflict.Start, conflict.End, conflict.AllDay, request);
            var other = conflict.Other is null
                ? I18N.Translate(request, "kleenestar.calendar:conflict.private")
                : $"{conflict.Other.Key} {conflict.Other.Summary}".Trim();

            return I18N.Translate
            (
                request,
                conflict.Kind == AppointmentConflictKind.Resource
                    ? "kleenestar.calendar:conflict.resource"
                    : "kleenestar.calendar:conflict.participant",
                conflict.SubjectName,
                when,
                other
            );
        }

        /// <summary>
        /// Checks a draft beyond what the dialog's inputs check: the resources are not booked
        /// twice, unless the draft books them anyway.
        /// </summary>
        /// <param name="draft">The draft.</param>
        /// <param name="objectId">The appointment being changed, or <see langword="null"/>.</param>
        /// <param name="request">The request.</param>
        /// <returns>The messages, keyed by input name; empty when the draft can be written.</returns>
        public static Dictionary<string, string> Check(AppointmentDraft draft, Guid? objectId, IRequest request)
        {
            var errors = new Dictionary<string, string>();

            if (draft.IgnoreConflicts || draft.ResourceIds.Count == 0)
            {
                return errors;
            }

            var taken = Conflicts(draft, objectId)
                .Where(x => x.Kind == AppointmentConflictKind.Resource)
                .Take(5)
                .Select(x => Describe(x, request))
                .ToList();

            if (taken.Count > 0)
            {
                errors[AppointmentForm.Resources] = string.Join(" ", taken) + " " + I18N.Translate(request, "kleenestar.calendar:conflict.override");
            }

            return errors;
        }

        /// <summary>
        /// Creates an appointment from a draft.
        /// </summary>
        /// <param name="class">The appointment class.</param>
        /// <param name="draft">The draft.</param>
        /// <param name="identityId">Who plans it.</param>
        /// <param name="presets">The presets of the template it is planned from, or <see langword="null"/>.</param>
        /// <returns>The appointment object.</returns>
        public static ObjectEntity Create(Class @class, AppointmentDraft draft, Guid identityId, IReadOnlyDictionary<string, string> presets)
        {
            var entity = AppointmentObjects.Create(@class, draft.Summary, draft.Description, identityId, presets);
            var organizer = identityId == Guid.Empty ? (Guid?)null : identityId;

            Write(entity, draft, organizer, identityId);

            return entity;
        }

        /// <summary>
        /// Changes an appointment after a draft.
        /// </summary>
        /// <param name="entity">The appointment object, read through the object manager.</param>
        /// <param name="draft">The draft.</param>
        /// <param name="identityId">Who changes it.</param>
        public static void Update(ObjectEntity entity, AppointmentDraft draft, Guid identityId)
        {
            var changed = !string.Equals(entity.Summary, draft.Summary, StringComparison.Ordinal)
                || (draft.Description is not null && !string.Equals(entity.Description ?? string.Empty, draft.Description, StringComparison.Ordinal));

            if (changed)
            {
                entity.Summary = draft.Summary;
                entity.Description = draft.Description ?? entity.Description;
                entity.Updated = DateTime.UtcNow;
                entity.UpdaterId = identityId == Guid.Empty ? null : identityId;

                using (CoreHub.CommitManager.BeginCommit(entity.Id, CommitType.Updated, identityId))
                {
                    CoreHub.ObjectManager.Update(entity);
                }
            }

            var organizer = CalendarHub.AppointmentManager.GetSchedule(entity.Id)?.OrganizerId;

            Write(entity, draft, organizer, identityId);
        }

        /// <summary>
        /// Writes the schedule, the participants and the bookings of a draft.
        /// </summary>
        /// <param name="entity">The appointment object.</param>
        /// <param name="draft">The draft.</param>
        /// <param name="organizer">The organizer, or <see langword="null"/>.</param>
        /// <param name="identityId">Who writes.</param>
        private static void Write(ObjectEntity entity, AppointmentDraft draft, Guid? organizer, Guid identityId)
        {
            var manager = CalendarHub.AppointmentManager;
            var schedule = manager.GetSchedule(entity.Id) ?? new AppointmentEntity { ObjectId = entity.Id };

            // a new rule starts without the exceptions of the old one
            if (!string.Equals(schedule.Recurrence, draft.Recurrence, StringComparison.Ordinal))
            {
                schedule.Exceptions = null;
            }

            schedule.Start = draft.Start;
            schedule.End = draft.End;
            schedule.AllDay = draft.AllDay;
            schedule.TimeZone = draft.TimeZone;
            schedule.Location = draft.Location;
            schedule.Recurrence = draft.Recurrence;
            schedule.AutoClose = draft.AutoClose;
            schedule.OrganizerId ??= organizer;

            manager.SaveSchedule(schedule, identityId);

            var stored = manager.GetParticipants(entity.Id);

            // a participant created with the object (its creator, by the manager) counts as
            // stored only once the dialog was saved; the dialog's list is what stands
            manager.SetParticipants(entity.Id, AppointmentForm.ParticipantsOf(draft, schedule.OrganizerId, stored));
            manager.SetResources(entity.Id, draft.ResourceIds);
        }
    }
}
