using KleeneStar.Core;
using KleeneStar.Core.WebControl;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WebExpress.WebIndex.Queries;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The outcome of an import of a calendar file.
    /// </summary>
    public sealed class AppointmentImportResult
    {
        /// <summary>
        /// Gets or sets the number of appointments created.
        /// </summary>
        public int Created { get; set; }

        /// <summary>
        /// Gets or sets the number of appointments updated (an event imported before, matched by
        /// its <c>UID</c>).
        /// </summary>
        public int Updated { get; set; }

        /// <summary>
        /// Gets or sets the number of events skipped (one the caller may not change, or one that
        /// could not be read).
        /// </summary>
        public int Skipped { get; set; }

        /// <summary>
        /// Gets the appointments created or updated.
        /// </summary>
        public List<ObjectEntity> Objects { get; } = [];
    }

    /// <summary>
    /// Translates between appointments and the events of iCalendar files: the export of a
    /// calendar or of a single object, and the import of a file into a workspace.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An exported appointment carries its schedule, series and exceptions, its place, its
    /// participants with their answers (an account by its mail address) and the address of its
    /// page; an object of another kind exported as an event is placed by the date fields of its
    /// class as whole days. The <c>UID</c> is the one an imported event came with, else
    /// <c>{object id}@kleenestar</c>, so a calendar that subscribed to an export updates rather
    /// than duplicates.
    /// </para>
    /// <para>
    /// An imported event updates the appointment that came from the same <c>UID</c> in the
    /// workspace, if the caller may change it, and is created in the chosen class otherwise. An
    /// attendee whose mail address belongs to an account is invited as that account, another
    /// as a guest; the answers come along. A replaced occurrence of a series
    /// (<c>RECURRENCE-ID</c>) becomes an appointment of its own and is removed from the series.
    /// </para>
    /// </remarks>
    public static class AppointmentExchange
    {
        /// <summary>
        /// The suffix of the identifier of an exported appointment that came from no file.
        /// </summary>
        public const string UidSuffix = "@kleenestar";

        /// <summary>
        /// Builds the event of an appointment.
        /// </summary>
        /// <param name="entity">The appointment object.</param>
        /// <param name="schedule">Its schedule.</param>
        /// <param name="participants">Its participants.</param>
        /// <param name="category">Its status category, or <see langword="null"/>.</param>
        /// <returns>The event.</returns>
        public static ICalendarEvent ToEvent(ObjectEntity entity, AppointmentEntity schedule, IEnumerable<AppointmentParticipant> participants, StatusCategory category)
        {
            var item = new ICalendarEvent
            {
                Uid = string.IsNullOrWhiteSpace(schedule.ExternalId) ? entity.Id + UidSuffix : schedule.ExternalId,
                Summary = entity.Summary,
                Description = Plain(entity.Description),
                Location = schedule.Location,
                Start = schedule.Start,
                End = schedule.End,
                AllDay = schedule.AllDay,
                TimeZone = schedule.TimeZone,
                Recurrence = schedule.Recurrence,
                Modified = entity.Updated,
                Url = Address(entity),
                Status = Status(category)
            };

            item.Exceptions.AddRange(AppointmentSchedule.Exceptions(schedule));

            var className = CoreHub.ClassManager.GetClass(entity.ClassId)?.Name;

            if (!string.IsNullOrWhiteSpace(className))
            {
                item.Categories.Add(className);
            }

            foreach (var participant in participants ?? [])
            {
                var identity = participant.IdentityId is Guid id ? CoreHub.IdentityManager?.GetIdentity(id) : null;
                var attendee = new ICalendarAttendee
                {
                    Name = identity?.Name ?? participant.Name,
                    Email = identity?.Email ?? participant.Email,
                    Role = participant.Role switch
                    {
                        AppointmentParticipantRole.Chair => "CHAIR",
                        AppointmentParticipantRole.Optional => "OPT-PARTICIPANT",
                        _ => "REQ-PARTICIPANT"
                    },
                    Status = participant.Response switch
                    {
                        AppointmentResponse.Accepted => "ACCEPTED",
                        AppointmentResponse.Tentative => "TENTATIVE",
                        AppointmentResponse.Declined => "DECLINED",
                        _ => "NEEDS-ACTION"
                    },
                    Comment = participant.Comment
                };

                item.Attendees.Add(attendee);

                if (schedule.OrganizerId is Guid organizer && participant.IdentityId == organizer)
                {
                    item.Organizer = new ICalendarAttendee { Name = attendee.Name, Email = attendee.Email };
                }
            }

            return item;
        }

        /// <summary>
        /// Builds the event of an object of another kind, placed by the date fields of its class
        /// as whole days.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <returns>The event, or <see langword="null"/> when its class has no date field.</returns>
        public static ICalendarEvent ToPlannedEvent(ObjectEntity entity)
        {
            var @class = CoreHub.ClassManager.GetClass(entity.ClassId);
            var context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);

            if (context?.StartDateField is null && context?.EndDateField is null)
            {
                return null;
            }

            var (start, end) = ObjectBoardProjection.ResolvePlan(entity, context);
            var zone = AppointmentTime.Zone(CalendarHub.Settings.TimeZone);

            var item = new ICalendarEvent
            {
                Uid = entity.Id + UidSuffix,
                Summary = $"{entity.Key} {entity.Summary}".Trim(),
                Description = Plain(entity.Description),
                Start = AppointmentTime.ToUtc(start.Date, zone),
                End = AppointmentTime.ToUtc(end.Date.AddDays(1), zone),
                AllDay = true,
                TimeZone = zone.Id,
                Modified = entity.Updated,
                Url = Address(entity)
            };

            if (!string.IsNullOrWhiteSpace(@class?.Name))
            {
                item.Categories.Add(@class.Name);
            }

            return item;
        }

        /// <summary>
        /// Builds the events of the appointments of a workspace: every appointment the caller may
        /// see, each once (a series with its rule), and with the layers the objects they show in
        /// the coming and the past year.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="layers">Whether the layers are included.</param>
        /// <returns>The events.</returns>
        public static IReadOnlyList<ICalendarEvent> Events(Workspace workspace, bool layers)
        {
            var manager = CalendarHub.AppointmentManager;
            var objects = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>()
                    .WhereEquals(x => x.WorkspaceId, workspace.Id)
                    .WhereEquals(x => x.Kind, ObjectKind.Appointment))
                .Where(x => x.State == WorkspaceState.Active)
                .ToList();

            var ids = new HashSet<Guid>(objects.Select(x => x.Id));

            if (layers)
            {
                // a layer is a view over a period; the export takes the year around today
                var now = DateTime.UtcNow;

                foreach (var entry in manager.GetEntries(workspace, now.AddYears(-1), now.AddYears(1)).Where(x => x.Source is not null))
                {
                    if (ids.Add(entry.Object.Id))
                    {
                        objects.Add(entry.Object);
                    }
                }
            }

            return Events(objects);
        }

        /// <summary>
        /// Builds the events of objects: appointments by their schedule, others by their date
        /// fields.
        /// </summary>
        /// <param name="objects">The objects, read through the object manager.</param>
        /// <returns>The events.</returns>
        public static IReadOnlyList<ICalendarEvent> Events(IReadOnlyList<ObjectEntity> objects)
        {
            var manager = CalendarHub.AppointmentManager;
            var appointments = objects.Where(x => string.Equals(x.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase)).ToList();
            var schedules = manager.GetSchedules(appointments.Select(x => x.Id));
            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var events = new List<ICalendarEvent>();

            foreach (var entity in objects)
            {
                if (string.Equals(entity.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase))
                {
                    if (!schedules.TryGetValue(entity.Id, out var schedule))
                    {
                        continue;
                    }

                    var @class = CoreHub.ClassManager.GetClass(entity.ClassId);
                    var context = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
                    var category = context?.WorkflowField is null ? null : ObjectBoardProjection.ResolveCategory(entity.Id, context, categories);

                    events.Add(ToEvent(entity, schedule, manager.GetParticipants(entity.Id), category));
                }
                else if (ToPlannedEvent(entity) is { } planned)
                {
                    events.Add(planned);
                }
            }

            return events;
        }

        /// <summary>
        /// Imports the events of a calendar file into a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="class">The appointment class new appointments are created in.</param>
        /// <param name="text">The content of the file.</param>
        /// <param name="identityId">Who imports, or <see cref="Guid.Empty"/>.</param>
        /// <param name="mayWrite">Answers whether the caller may change an appointment imported before.</param>
        /// <param name="zone">The zone floating times are taken in.</param>
        /// <returns>The outcome.</returns>
        public static AppointmentImportResult Import(Workspace workspace, Class @class, string text, Guid identityId, Func<ObjectEntity, bool> mayWrite, TimeZoneInfo zone)
        {
            var result = new AppointmentImportResult();
            var manager = CalendarHub.AppointmentManager;
            var events = ICalendar.Read(text, zone?.Id);

            // the masters first, so a replaced occurrence finds its series
            foreach (var item in events.OrderBy(x => x.RecurrenceId is null ? 0 : 1))
            {
                try
                {
                    var uid = string.IsNullOrWhiteSpace(item.Uid) ? null : item.Uid.Trim();
                    var externalId = item.RecurrenceId is null || uid is null
                        ? uid
                        : $"{uid}#{AppointmentSchedule.FormatInstant(item.RecurrenceId.Value)}";

                    var existing = externalId is null ? null : manager.FindByExternalId(workspace.Id, externalId);

                    // an event this installation exported names its appointment by id
                    if (existing is null && item.RecurrenceId is null && OwnId(uid) is Guid ownId
                        && CoreHub.ObjectManager.GetObject(ownId) is { } own && own.WorkspaceId == workspace.Id)
                    {
                        existing = manager.GetSchedule(ownId);
                        externalId = existing?.ExternalId;
                    }
                    var entity = existing is null ? null : CoreHub.ObjectManager.GetObject(existing.ObjectId);

                    if (existing is not null && (entity is null || !mayWrite(entity)))
                    {
                        result.Skipped++;
                        continue;
                    }

                    var summary = string.IsNullOrWhiteSpace(item.Summary) ? "?" : item.Summary.Trim();
                    var description = string.IsNullOrWhiteSpace(item.Description) ? null : ProseDocument(item.Description);

                    if (entity is null)
                    {
                        entity = AppointmentObjects.Create(@class, summary, description, identityId);
                        result.Created++;
                    }
                    else
                    {
                        if (!string.Equals(entity.Summary, summary, StringComparison.Ordinal) || (description is not null && !string.Equals(entity.Description, description, StringComparison.Ordinal)))
                        {
                            entity.Summary = summary;
                            entity.Description = description ?? entity.Description;
                            entity.Updated = DateTime.UtcNow;
                            entity.UpdaterId = identityId == Guid.Empty ? null : identityId;
                            CoreHub.ObjectManager.Update(entity);
                        }

                        result.Updated++;
                    }

                    var schedule = manager.GetSchedule(entity.Id) ?? new AppointmentEntity { ObjectId = entity.Id };
                    schedule.Start = item.Start;
                    schedule.End = item.End;
                    schedule.AllDay = item.AllDay;
                    schedule.TimeZone = AppointmentTime.Zone(item.TimeZone ?? zone?.Id).Id;
                    schedule.Location = item.Location;
                    schedule.Recurrence = item.RecurrenceId is null && RecurrenceRule.TryParse(item.Recurrence, out var rule, out _) ? rule.ToString() : null;
                    schedule.Exceptions = schedule.Recurrence is null ? null : AppointmentSchedule.FormatExceptions(item.Exceptions);
                    schedule.ExternalId = externalId;
                    schedule.OrganizerId ??= identityId == Guid.Empty ? null : identityId;

                    manager.SaveSchedule(schedule, identityId);

                    var participants = item.Attendees.Select(ToParticipant).Where(x => x is not null).ToList();

                    if (participants.Count > 0)
                    {
                        manager.SetParticipants(entity.Id, participants);

                        // the answers of the file stand for the answers of the accounts
                        foreach (var participant in participants.Where(x => x.IdentityId is not null && x.Response != AppointmentResponse.Pending))
                        {
                            manager.Respond(entity.Id, participant.IdentityId.Value, participant.Response, participant.Comment);
                        }
                    }

                    // a replaced occurrence is no longer part of its series
                    if (item.RecurrenceId is not null && uid is not null
                        && manager.FindByExternalId(workspace.Id, uid) is { } master)
                    {
                        manager.ExcludeOccurrence(master.ObjectId, item.RecurrenceId.Value);
                    }

                    result.Objects.Add(entity);
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    CalendarHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);
                }
            }

            return result;
        }

        /// <summary>
        /// Reads the id of an appointment from the identifier this installation exports it under
        /// (<c>{object id}@kleenestar</c>).
        /// </summary>
        /// <param name="uid">The identifier of the event.</param>
        /// <returns>The object id, or <see langword="null"/>.</returns>
        public static Guid? OwnId(string uid)
        {
            return uid is not null && uid.EndsWith(UidSuffix, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(uid[..^UidSuffix.Length], out var id)
                    ? id
                    : null;
        }

        /// <summary>
        /// Turns an attendee of a file into a participant: an account when the mail address
        /// belongs to one, a guest otherwise.
        /// </summary>
        /// <param name="attendee">The attendee.</param>
        /// <returns>The participant, or <see langword="null"/> for an attendee without name and
        /// address.</returns>
        public static AppointmentParticipant ToParticipant(ICalendarAttendee attendee)
        {
            if (attendee is null || (string.IsNullOrWhiteSpace(attendee.Email) && string.IsNullOrWhiteSpace(attendee.Name)))
            {
                return null;
            }

            var identity = string.IsNullOrWhiteSpace(attendee.Email)
                ? null
                : CoreHub.IdentityManager?
                    .GetIdentities(new Query<Identity>().WhereEquals(x => x.Email, attendee.Email.Trim()))
                    .FirstOrDefault();

            return new AppointmentParticipant
            {
                IdentityId = identity?.Id,
                Name = identity?.Name ?? attendee.Name ?? attendee.Email,
                Email = identity is null ? attendee.Email : null,
                Role = attendee.Role switch
                {
                    "CHAIR" => AppointmentParticipantRole.Chair,
                    "OPT-PARTICIPANT" or "NON-PARTICIPANT" => AppointmentParticipantRole.Optional,
                    _ => AppointmentParticipantRole.Required
                },
                Response = attendee.Status switch
                {
                    "ACCEPTED" => AppointmentResponse.Accepted,
                    "TENTATIVE" => AppointmentResponse.Tentative,
                    "DECLINED" => AppointmentResponse.Declined,
                    _ => AppointmentResponse.Pending
                },
                Comment = attendee.Comment
            };
        }

        /// <summary>
        /// Turns plain text into an editor document, one paragraph per line, as every description
        /// field stores it.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The document as JSON.</returns>
        public static string ProseDocument(string text)
        {
            var paragraphs = (text ?? string.Empty)
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Length == 0
                    ? (object)new { type = "paragraph" }
                    : new { type = "paragraph", content = new object[] { new { type = "text", text = line } } })
                .ToArray();

            return JsonSerializer.Serialize(new { version = 1, doc = new { type = "doc", content = paragraphs } });
        }

        /// <summary>
        /// Answers the plain text of a description.
        /// </summary>
        /// <param name="description">The stored description.</param>
        /// <returns>The text, or <see langword="null"/>.</returns>
        private static string Plain(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return null;
            }

            var text = ProseText.ToPlainText(description);

            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        /// <summary>
        /// Answers the absolute address of the page of an object.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <returns>The address, or <see langword="null"/>.</returns>
        private static string Address(ObjectEntity entity)
        {
            string uri;

            try
            {
                uri = CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString();
            }
            catch (NullReferenceException)
            {
                // without a sitemap (a host that is starting, a test) the event has no address
                return null;
            }

            if (string.IsNullOrWhiteSpace(uri))
            {
                return null;
            }

            var root = CoreHub.HttpServerContext?.ExternalUri?.ToString()?.TrimEnd('/');

            return uri.StartsWith("http", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(root) ? uri : root + uri;
        }

        /// <summary>
        /// Answers the status of an event from the status category of its appointment.
        /// </summary>
        /// <param name="category">The category, or <see langword="null"/>.</param>
        /// <returns>The status.</returns>
        private static string Status(StatusCategory category)
        {
            if (category is null)
            {
                return "CONFIRMED";
            }

            var name = (category.Name ?? string.Empty).ToLowerInvariant();

            return name.Contains("cancel") || name.Contains("abgesagt") ? "CANCELLED" : "CONFIRMED";
        }
    }
}
