using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebQuickfilter;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using WebExpress.WebCore;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebIndex.Queries;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebManager
{
    /// <summary>
    /// The implementation of <see cref="IAppointmentManager"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The schedules, participants and bookings are read from the model directly, because they
    /// carry no visibility of their own; the objects they belong to are always read through the
    /// core's object manager, which narrows to what the caller may see. A conflict is the one
    /// question asked across that boundary: whether a time is taken is answered from every
    /// booking, but what it is taken by is shown only to who may see it.
    /// </para>
    /// <para>
    /// Closing an appointment whose time is over is a transition of its workflow like any other,
    /// recorded without actor: it is the system that closes it, not a person.
    /// </para>
    /// </remarks>
    public sealed class AppointmentManager : IAppointmentManager
    {
        /// <summary>
        /// The most objects a query layer shows: a query that selects a whole installation must
        /// not take the calendar down with it.
        /// </summary>
        public const int MaxQueryObjects = 2000;

        /// <summary>
        /// The most occurrences of a planned series checked for conflicts.
        /// </summary>
        public const int MaxConflictOccurrences = 200;

        /// <summary>
        /// How far ahead the occurrences of an open series are checked for conflicts.
        /// </summary>
        public static readonly TimeSpan ConflictHorizon = TimeSpan.FromDays(366);

        private readonly IHttpServerContext _httpServerContext;
        private readonly object _gate = new();
        private bool _connected;

        /// <inheritdoc/>
        public event EventHandler<AppointmentEntity> ScheduleChanged;

        /// <inheritdoc/>
        public event EventHandler<Guid> ParticipantsChanged;

        /// <inheritdoc/>
        public event EventHandler<AppointmentParticipant> ParticipantResponded;

        /// <inheritdoc/>
        public event EventHandler<AppointmentCalendarSource> SourceAdded;

        /// <inheritdoc/>
        public event EventHandler<AppointmentCalendarSource> SourceUpdated;

        /// <inheritdoc/>
        public event EventHandler<AppointmentCalendarSource> SourceRemoved;

        /// <inheritdoc/>
        public event EventHandler<AppointmentEntity> AppointmentClosed;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="componentHub">The component hub.</param>
        /// <param name="httpServerContext">The server context, for the log.</param>
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Used via reflection.")]
        private AppointmentManager(IComponentHub componentHub, IHttpServerContext httpServerContext)
        {
            _httpServerContext = httpServerContext;
        }

        /// <inheritdoc/>
        public void Connect()
        {
            lock (_gate)
            {
                if (_connected)
                {
                    return;
                }

                _connected = true;
            }

            CoreHub.ObjectManager.ObjectAdded += (_, entity) => Capture(() => OnObjectAdded(entity));
            CoreHub.ObjectManager.ObjectRemoved += (_, entity) => Capture(() => OnObjectRemoved(entity));

            ConnectAudit();
        }

        /// <inheritdoc/>
        public TimeZoneInfo GetDisplayZone(Guid identityId)
        {
            var identity = identityId == Guid.Empty ? null : CoreHub.IdentityManager?.GetIdentity(identityId);

            if (!string.IsNullOrWhiteSpace(identity?.TimeZone))
            {
                return AppointmentTime.Zone(identity.TimeZone);
            }

            return AppointmentTime.Zone(CalendarHub.Settings.TimeZone);
        }

        /// <inheritdoc/>
        public AppointmentEntity GetSchedule(Guid objectId)
        {
            return objectId == Guid.Empty ? null : ModelHub.GetAppointment(objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyDictionary<Guid, AppointmentEntity> GetSchedules(IEnumerable<Guid> objectIds)
        {
            return ModelHub.GetAppointments(objectIds);
        }

        /// <inheritdoc/>
        public AppointmentEntity FindByExternalId(Guid workspaceId, string externalId)
        {
            return ModelHub.GetAppointmentByExternalId(workspaceId, externalId);
        }

        /// <inheritdoc/>
        public AppointmentEntity SaveSchedule(AppointmentEntity schedule, Guid actorId)
        {
            ArgumentNullException.ThrowIfNull(schedule);

            if (schedule.ObjectId == Guid.Empty)
            {
                throw new InvalidOperationException("A schedule belongs to an appointment object.");
            }

            AppointmentSchedule.Normalize(schedule);

            var stored = ModelHub.GetAppointment(schedule.ObjectId);

            // a new time is a new question: what was answered to the old one no longer holds
            var moved = stored is not null
                && (stored.Start != schedule.Start
                    || stored.End != schedule.End
                    || stored.AllDay != schedule.AllDay
                    || !string.Equals(stored.Recurrence, schedule.Recurrence, StringComparison.Ordinal));

            // an appointment closed for being over opens again when it is moved into the future
            if (schedule.Closed is not null && (schedule.SeriesEnd is null || schedule.SeriesEnd > DateTime.UtcNow))
            {
                schedule.Closed = null;
            }

            ModelHub.SaveAppointment(schedule);

            if (moved)
            {
                ModelHub.ResetAppointmentResponses(schedule.ObjectId, actorId == Guid.Empty ? null : actorId);
            }

            ScheduleChanged?.Invoke(this, schedule);

            return schedule;
        }

        /// <inheritdoc/>
        public bool ExcludeOccurrence(Guid objectId, DateTime occurrenceStart)
        {
            var schedule = GetSchedule(objectId);

            if (schedule is null || string.IsNullOrWhiteSpace(schedule.Recurrence))
            {
                return false;
            }

            var start = DateTime.SpecifyKind(occurrenceStart, DateTimeKind.Utc);
            var exists = AppointmentSchedule
                .Occurrences(schedule, start.AddTicks(-1), start.AddTicks(1), 1)
                .Any(x => x.Start == start);

            if (!exists)
            {
                return false;
            }

            var exceptions = AppointmentSchedule.Exceptions(schedule);
            exceptions.Add(start);
            schedule.Exceptions = AppointmentSchedule.FormatExceptions(exceptions);

            // the exceptions do not move the series, so the answers stay
            AppointmentSchedule.Normalize(schedule);
            ModelHub.SaveAppointment(schedule);
            ScheduleChanged?.Invoke(this, schedule);

            return true;
        }

        /// <inheritdoc/>
        public IReadOnlyList<AppointmentParticipant> GetParticipants(Guid objectId)
        {
            return objectId == Guid.Empty ? [] : ModelHub.GetAppointmentParticipants(objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<AppointmentParticipant> SetParticipants(Guid objectId, IEnumerable<AppointmentParticipant> participants)
        {
            var list = (participants ?? [])
                .Where(x => x is not null && (x.IdentityId is not null || !string.IsNullOrWhiteSpace(x.Name) || !string.IsNullOrWhiteSpace(x.Email)))
                .Where(x => x.IdentityId != Guid.Empty)
                .ToList();

            // an account named twice is invited once
            list = [.. list
                .GroupBy(x => x.IdentityId is null ? "guest:" + (x.Email ?? x.Name)?.ToLowerInvariant() : x.IdentityId.ToString())
                .Select(x => x.First())];

            var added = ModelHub.SetAppointmentParticipants(objectId, list);

            // an account invited follows the appointment as a watcher, so it hears of changes
            foreach (var identityId in added.Where(x => x.IdentityId is not null).Select(x => x.IdentityId.Value))
            {
                Capture(() =>
                {
                    if (!CoreHub.WatcherManager.GetWatchers(objectId).Any(w => w.IdentityId == identityId))
                    {
                        CoreHub.WatcherManager.Add(objectId, identityId);
                    }
                });
            }

            ParticipantsChanged?.Invoke(this, objectId);

            return added;
        }

        /// <inheritdoc/>
        public AppointmentParticipant Respond(Guid objectId, Guid identityId, AppointmentResponse response, string comment)
        {
            if (objectId == Guid.Empty || identityId == Guid.Empty)
            {
                return null;
            }

            var participant = ModelHub.RespondToAppointment(objectId, identityId, response, comment);

            if (participant is not null)
            {
                ParticipantResponded?.Invoke(this, participant);
            }

            return participant;
        }

        /// <inheritdoc/>
        public IReadOnlyList<Guid> GetResources(Guid objectId)
        {
            return ModelHub.GetAppointmentResources([objectId]).TryGetValue(objectId, out var resources) ? resources : [];
        }

        /// <inheritdoc/>
        public void SetResources(Guid objectId, IEnumerable<Guid> resourceIds)
        {
            ModelHub.SetAppointmentResources(objectId, resourceIds);
            ParticipantsChanged?.Invoke(this, objectId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<AppointmentCalendarSource> GetSources(Guid workspaceId)
        {
            return workspaceId == Guid.Empty ? [] : ModelHub.GetAppointmentCalendarSources(workspaceId);
        }

        /// <inheritdoc/>
        public AppointmentCalendarSource GetSource(Guid sourceId)
        {
            return sourceId == Guid.Empty ? null : ModelHub.GetAppointmentCalendarSource(sourceId);
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> Validate(AppointmentCalendarSource source)
        {
            var problems = new List<string>();

            if (source is null)
            {
                problems.Add("kleenestar.calendar:source.invalid");
                return problems;
            }

            if (string.IsNullOrWhiteSpace(source.Name))
            {
                problems.Add("kleenestar.calendar:source.name.required");
            }

            if (string.Equals(source.Kind, AppointmentCalendarSource.WorkspaceKind, StringComparison.OrdinalIgnoreCase))
            {
                if (source.SourceWorkspaceId is null || CoreHub.WorkspaceManager.GetWorkspace(source.SourceWorkspaceId.Value) is null)
                {
                    problems.Add("kleenestar.calendar:source.workspace.required");
                }
                else if (source.SourceWorkspaceId == source.WorkspaceId)
                {
                    problems.Add("kleenestar.calendar:source.workspace.self");
                }
            }
            else if (string.Equals(source.Kind, AppointmentCalendarSource.QueryKind, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(source.Query))
                {
                    problems.Add("kleenestar.calendar:source.query.required");
                }
                else if (!WqlFilter.TryValidate<ObjectEntity>(source.Query, out _))
                {
                    problems.Add("kleenestar.calendar:source.query.invalid");
                }
            }
            else
            {
                problems.Add("kleenestar.calendar:source.kind.required");
            }

            return problems;
        }

        /// <inheritdoc/>
        public IAppointmentManager AddSource(AppointmentCalendarSource source)
        {
            Require(source);

            source.Id = source.Id == Guid.Empty ? Guid.NewGuid() : source.Id;
            source.Kind = source.Kind.Trim().ToLowerInvariant();
            source.Created = source.Created == default ? DateTime.UtcNow : source.Created;
            source.Updated = DateTime.UtcNow;

            if (source.Order == 0)
            {
                source.Order = GetSources(source.WorkspaceId).Select(x => x.Order).DefaultIfEmpty(0).Max() + 1;
            }

            Clean(source);
            ModelHub.AddAppointmentCalendarSource(source);
            SourceAdded?.Invoke(this, source);

            return this;
        }

        /// <inheritdoc/>
        public IAppointmentManager UpdateSource(AppointmentCalendarSource source)
        {
            Require(source);

            source.Kind = source.Kind.Trim().ToLowerInvariant();
            source.Updated = DateTime.UtcNow;

            Clean(source);
            ModelHub.UpdateAppointmentCalendarSource(source);
            SourceUpdated?.Invoke(this, source);

            return this;
        }

        /// <inheritdoc/>
        public IAppointmentManager RemoveSource(Guid sourceId)
        {
            var source = GetSource(sourceId);

            if (source is null)
            {
                return this;
            }

            ModelHub.RemoveAppointmentCalendarSource(sourceId);
            SourceRemoved?.Invoke(this, source);

            return this;
        }

        /// <inheritdoc/>
        public IReadOnlyList<CalendarEntry> GetEntries(Workspace workspace, DateTime from, DateTime to, bool layers = true)
        {
            if (workspace is null || to <= from)
            {
                return [];
            }

            var entries = new List<CalendarEntry>();

            entries.AddRange(WorkspaceEntries(workspace.Id, from, to, null));

            if (layers)
            {
                foreach (var source in GetSources(workspace.Id).Where(x => x.Enabled))
                {
                    try
                    {
                        if (string.Equals(source.Kind, AppointmentCalendarSource.WorkspaceKind, StringComparison.OrdinalIgnoreCase))
                        {
                            if (source.SourceWorkspaceId is Guid sourceWorkspaceId && sourceWorkspaceId != workspace.Id)
                            {
                                entries.AddRange(WorkspaceEntries(sourceWorkspaceId, from, to, source));
                            }
                        }
                        else if (string.Equals(source.Kind, AppointmentCalendarSource.QueryKind, StringComparison.OrdinalIgnoreCase))
                        {
                            entries.AddRange(QueryEntries(source, from, to));
                        }
                    }
                    catch (Exception ex)
                    {
                        // one broken layer must not take the calendar down
                        _httpServerContext?.Log?.Exception(ex);
                    }
                }
            }

            // an object several layers show appears once, in the first of them
            return [.. entries
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .OrderBy(x => x.Start)
                .ThenBy(x => x.Object?.Summary, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <inheritdoc/>
        public IReadOnlyList<CalendarEntry> GetUpcoming(Workspace workspace, DateTime now, TimeSpan horizon, int limit)
        {
            return [.. GetEntries(workspace, now, now + horizon)
                .Where(x => x.End > now || (x.End == x.Start && x.Start >= now))
                .Where(x => !IsDone(x.Category))
                .OrderBy(x => x.Start)
                .Take(Math.Max(1, limit))];
        }

        /// <inheritdoc/>
        public IReadOnlyList<AppointmentConflict> FindConflicts(AppointmentConflictQuery query)
        {
            if (query is null || (query.IdentityIds.Count == 0 && query.ResourceIds.Count == 0))
            {
                return [];
            }

            var planned = new AppointmentEntity
            {
                ObjectId = query.ObjectId ?? Guid.Empty,
                Start = DateTime.SpecifyKind(query.Start, DateTimeKind.Utc),
                End = DateTime.SpecifyKind(query.End < query.Start ? query.Start : query.End, DateTimeKind.Utc),
                AllDay = query.AllDay,
                TimeZone = query.TimeZone,
                Recurrence = query.Recurrence,
                Exceptions = query.Exceptions
            };

            var mine = AppointmentSchedule
                .Occurrences(planned, planned.Start.AddTicks(-1), planned.Start + ConflictHorizon, MaxConflictOccurrences)
                .ToList();

            if (mine.Count == 0)
            {
                return [];
            }

            var from = mine.Min(x => x.Start);
            var to = mine.Max(x => x.End == x.Start ? x.End.AddTicks(1) : x.End);

            var identities = query.IdentityIds.Where(x => x != Guid.Empty).ToHashSet();
            var resources = query.ResourceIds.Where(x => x != Guid.Empty).ToHashSet();

            var candidates = ModelHub.GetAppointmentsAttendedBy(identities)
                .Concat(ModelHub.GetAppointmentsBooking(resources))
                .Where(x => x != query.ObjectId)
                .Distinct()
                .ToList();

            if (candidates.Count == 0)
            {
                return [];
            }

            var schedules = ModelHub.GetAppointmentsOfObjectsInPeriod(candidates, from, to);

            if (schedules.Count == 0)
            {
                return [];
            }

            var ids = schedules.Select(x => x.ObjectId).ToList();
            var participants = ModelHub.GetAppointmentParticipants(ids);
            var bookings = ModelHub.GetAppointmentResources(ids);

            // whether a time is taken does not depend on who asks; what it is taken by does
            IReadOnlyDictionary<Guid, ObjectEntity> all;

            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                all = ReadObjects(ids);
            }

            var visible = ReadObjects(ids);
            var categories = Categories(all.Values);

            var conflicts = new List<AppointmentConflict>();
            var names = new Dictionary<Guid, string>();

            foreach (var schedule in schedules)
            {
                if (!all.TryGetValue(schedule.ObjectId, out var other) || other.State != WorkspaceState.Active)
                {
                    continue;
                }

                // a cancelled appointment holds nobody and nothing
                if (categories.TryGetValue(other.Id, out var category) && IsDone(category) && (schedule.SeriesEnd is null || schedule.SeriesEnd > DateTime.UtcNow))
                {
                    continue;
                }

                var theirs = AppointmentSchedule.Occurrences(schedule, from, to, MaxConflictOccurrences * 4).ToList();

                participants.TryGetValue(schedule.ObjectId, out var attending);
                bookings.TryGetValue(schedule.ObjectId, out var booked);

                var busy = (attending ?? [])
                    .Where(x => x.IdentityId is not null && x.Response != AppointmentResponse.Declined && identities.Contains(x.IdentityId.Value))
                    .Select(x => (Kind: AppointmentConflictKind.Participant, Id: x.IdentityId.Value, Name: x.Name))
                    .Concat((booked ?? []).Where(resources.Contains).Select(x => (Kind: AppointmentConflictKind.Resource, Id: x, Name: (string)null)))
                    .ToList();

                if (busy.Count == 0)
                {
                    continue;
                }

                foreach (var occurrence in theirs)
                {
                    if (!mine.Any(x => Overlap(x, occurrence)))
                    {
                        continue;
                    }

                    foreach (var (kind, subjectId, storedName) in busy)
                    {
                        if (!names.TryGetValue(subjectId, out var name))
                        {
                            name = kind == AppointmentConflictKind.Participant
                                ? CoreHub.IdentityManager?.GetIdentity(subjectId)?.Name ?? storedName
                                : ResourceName(subjectId);
                            names[subjectId] = name;
                        }

                        conflicts.Add(new AppointmentConflict
                        {
                            Kind = kind,
                            SubjectId = subjectId,
                            SubjectName = name,
                            OtherObjectId = schedule.ObjectId,
                            Other = visible.TryGetValue(schedule.ObjectId, out var shown) ? shown : null,
                            Start = occurrence.Start,
                            End = occurrence.End,
                            AllDay = occurrence.AllDay
                        });
                    }
                }
            }

            return [.. conflicts
                .OrderBy(x => x.Start)
                .ThenBy(x => x.Kind)
                .ThenBy(x => x.SubjectName, StringComparer.CurrentCultureIgnoreCase)
                .Take(100)];
        }

        /// <inheritdoc/>
        public int CloseExpired(DateTime now)
        {
            var grace = TimeSpan.FromMinutes(CalendarHub.Settings.AutoCloseGrace);
            var closed = 0;

            // the closing is the system's own business, and an appointment classified above the
            // reach of anybody would otherwise never be closed
            using var unrestricted = CoreHub.SecurityLevelManager?.BeginUnrestricted();

            foreach (var schedule in ModelHub.GetAppointmentsToClose(now - grace, 200))
            {
                try
                {
                    if (Close(schedule))
                    {
                        closed++;
                    }

                    // closed, or nothing to close it into: either way it is not looked at again
                    schedule.Closed = now;
                    ModelHub.SaveAppointment(schedule);
                }
                catch (Exception ex)
                {
                    _httpServerContext?.Log?.Exception(ex);
                }
            }

            return closed;
        }

        /// <summary>
        /// Moves one appointment into a closing state of its workflow.
        /// </summary>
        /// <param name="schedule">The schedule of the appointment.</param>
        /// <returns><see langword="true"/> when the appointment was moved.</returns>
        private bool Close(AppointmentEntity schedule)
        {
            var entity = CoreHub.ObjectManager.GetObject(schedule.ObjectId);

            if (entity is null || entity.State != WorkspaceState.Active)
            {
                return false;
            }

            var field = ClassFieldsOf(entity.ClassId).FirstOrDefault(x => x.FieldType == FieldType.Workflow && x.WorkflowId is not null);

            if (field is null)
            {
                return false;
            }

            var workflow = CoreHub.WorkflowManager.GetWorkflowWithStructure(field.WorkflowId.Value);

            if (workflow is null)
            {
                return false;
            }

            var current = CoreHub.WorkflowManager.ResolveStatus(workflow, CoreHub.ValueManager.GetValue(entity.Id, field.Id)?.Data);
            var categories = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);

            bool Done(Status status) => status is not null
                && categories.TryGetValue(status.CategoryId, out var category)
                && IsDone(category);

            if (Done(current))
            {
                return false;
            }

            // the first closing state the workflow leads to from here - the order of the states
            // is the template's, which puts "held" before "cancelled"
            var target = (current is null ? workflow.Statuses : CoreHub.WorkflowManager.GetTargetStatuses(workflow, current))?
                .Where(Done)
                .FirstOrDefault();

            if (target is null)
            {
                return false;
            }

            var result = CoreHub.WorkflowManager.ExecuteTransition(entity.Id, field.Id, target.Id, Guid.Empty);

            if (result?.Outcome != Core.WebManager.WorkflowTransitionOutcome.Executed)
            {
                return false;
            }

            AppointmentClosed?.Invoke(this, schedule);

            return true;
        }

        /// <summary>
        /// Answers the entries of the appointments of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <param name="source">The layer the workspace is shown through, or <see langword="null"/>.</param>
        /// <returns>The entries.</returns>
        private static IEnumerable<CalendarEntry> WorkspaceEntries(Guid workspaceId, DateTime from, DateTime to, AppointmentCalendarSource source)
        {
            var schedules = ModelHub.GetAppointmentsOfWorkspacesInPeriod([workspaceId], from, to);

            return Entries(schedules, from, to, source);
        }

        /// <summary>
        /// Answers the entries of a query layer: the occurrences of the appointments it selects,
        /// and the other objects it selects placed by the date fields of their class.
        /// </summary>
        /// <param name="source">The layer.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <returns>The entries.</returns>
        private static IEnumerable<CalendarEntry> QueryEntries(AppointmentCalendarSource source, DateTime from, DateTime to)
        {
            var predicate = WqlFilter.Compile<ObjectEntity>(source.Query);

            if (predicate is null)
            {
                return [];
            }

            var objects = CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().Where(predicate).WithPaging(0, MaxQueryObjects))
                .Where(x => x.State == WorkspaceState.Active)
                .ToList();

            var entries = new List<CalendarEntry>();

            var appointments = objects.Where(x => string.Equals(x.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase)).ToList();

            if (appointments.Count > 0)
            {
                var schedules = ModelHub.GetAppointmentsOfObjectsInPeriod(appointments.Select(x => x.Id), from, to);
                entries.AddRange(Entries(schedules, from, to, source, appointments.ToDictionary(x => x.Id)));
            }

            var others = objects.Where(x => !string.Equals(x.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase)).ToList();

            if (others.Count > 0)
            {
                entries.AddRange(PlannedEntries(others, from, to, source));
            }

            return entries;
        }

        /// <summary>
        /// Answers the entries of schedules: their occurrences in the period, for the objects the
        /// caller may see.
        /// </summary>
        /// <param name="schedules">The schedules.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <param name="source">The layer, or <see langword="null"/>.</param>
        /// <param name="known">The objects read already, or <see langword="null"/>.</param>
        /// <returns>The entries.</returns>
        private static IEnumerable<CalendarEntry> Entries(IReadOnlyList<AppointmentEntity> schedules, DateTime from, DateTime to, AppointmentCalendarSource source, IReadOnlyDictionary<Guid, ObjectEntity> known = null)
        {
            if (schedules.Count == 0)
            {
                return [];
            }

            var objects = known ?? ReadObjects(schedules.Select(x => x.ObjectId));
            var categories = Categories(objects.Values);
            var entries = new List<CalendarEntry>();

            foreach (var schedule in schedules)
            {
                if (!objects.TryGetValue(schedule.ObjectId, out var entity) || entity.State != WorkspaceState.Active)
                {
                    continue;
                }

                categories.TryGetValue(entity.Id, out var category);

                foreach (var occurrence in AppointmentSchedule.Occurrences(schedule, from, to))
                {
                    entries.Add(new CalendarEntry
                    {
                        Object = entity,
                        Start = occurrence.Start,
                        End = occurrence.End,
                        AllDay = occurrence.AllDay,
                        Recurring = occurrence.Recurring,
                        Location = schedule.Location,
                        Source = source,
                        Category = category,
                        IsAppointment = true
                    });
                }
            }

            return entries;
        }

        /// <summary>
        /// Answers the entries of objects of other kinds, placed by the start and end date
        /// fields of their class as whole days; an object whose class has no date field is not
        /// placed, because it has no time to be shown at.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <param name="source">The layer.</param>
        /// <returns>The entries.</returns>
        private static IEnumerable<CalendarEntry> PlannedEntries(IReadOnlyList<ObjectEntity> objects, DateTime from, DateTime to, AppointmentCalendarSource source)
        {
            var contexts = new Dictionary<Guid, ObjectBoardClassContext>();

            foreach (var classId in objects.Select(x => x.ClassId).Distinct())
            {
                var @class = CoreHub.ClassManager.GetClass(classId);
                contexts[classId] = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
            }

            var read = ObjectBoardProjection.ReadValues(objects, contexts.Values.Where(x => x is not null));
            var categoriesById = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var zone = AppointmentTime.Zone(CalendarHub.Settings.TimeZone);
            var entries = new List<CalendarEntry>();

            foreach (var entity in objects)
            {
                var context = contexts[entity.ClassId];

                // an object without a date is not planned; the creation day the boards fall back
                // to is no time it takes place at
                if (ObjectBoardProjection.ParseDate(read(entity.Id, context?.StartDateField)) is null
                    && ObjectBoardProjection.ParseDate(read(entity.Id, context?.EndDateField)) is null)
                {
                    continue;
                }

                var (start, end) = ObjectBoardProjection.ResolvePlan(entity, context, read);
                var firstDay = start.Kind == DateTimeKind.Utc ? AppointmentTime.ToLocal(start, zone).Date : start.Date;
                var lastDay = end.Kind == DateTimeKind.Utc ? AppointmentTime.ToLocal(end, zone).Date : end.Date;

                var entry = new CalendarEntry
                {
                    Object = entity,
                    Start = AppointmentTime.ToUtc(firstDay, zone),
                    End = AppointmentTime.ToUtc(lastDay.AddDays(1), zone),
                    AllDay = true,
                    Source = source,
                    Category = ObjectBoardProjection.ResolveCategory(read(entity.Id, context.WorkflowField), context, categoriesById),
                    IsAppointment = false
                };

                if (entry.Start < to && entry.End > from)
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        /// <summary>
        /// Reads objects through the object manager, so that only those the caller may see are
        /// answered.
        /// </summary>
        /// <param name="ids">The objects.</param>
        /// <returns>The objects found, keyed by id.</returns>
        private static IReadOnlyDictionary<Guid, ObjectEntity> ReadObjects(IEnumerable<Guid> ids)
        {
            var list = (ids ?? []).Distinct().ToList();

            if (list.Count == 0)
            {
                return new Dictionary<Guid, ObjectEntity>();
            }

            return CoreHub.ObjectManager
                .GetObjects(new Query<ObjectEntity>().Where(x => list.Contains(x.Id)))
                .GroupBy(x => x.Id)
                .ToDictionary(x => x.Key, x => x.First());
        }

        /// <summary>
        /// Resolves the status categories of objects from their workflow fields.
        /// </summary>
        /// <param name="objects">The objects.</param>
        /// <returns>The categories, keyed by object; an object without workflow is missing.</returns>
        private static IReadOnlyDictionary<Guid, StatusCategory> Categories(IEnumerable<ObjectEntity> objects)
        {
            var list = objects.ToList();

            if (list.Count == 0)
            {
                return new Dictionary<Guid, StatusCategory>();
            }

            var contexts = new Dictionary<Guid, ObjectBoardClassContext>();

            foreach (var classId in list.Select(x => x.ClassId).Distinct())
            {
                var @class = CoreHub.ClassManager.GetClass(classId);
                contexts[classId] = @class is null ? null : ObjectBoardProjection.BuildClassContext(@class);
            }

            var read = ObjectBoardProjection.ReadValues(list, contexts.Values.Where(x => x is not null));
            var categoriesById = ObjectBoardProjection.GetOrderedCategories().ToDictionary(x => x.Id);
            var result = new Dictionary<Guid, StatusCategory>();

            foreach (var entity in list)
            {
                var context = contexts[entity.ClassId];

                if (context?.WorkflowField is null)
                {
                    continue;
                }

                var category = ObjectBoardProjection.ResolveCategory(read(entity.Id, context.WorkflowField), context, categoriesById);

                if (category is not null)
                {
                    result[entity.Id] = category;
                }
            }

            return result;
        }

        /// <summary>
        /// Determines whether a status category closes an object.
        /// </summary>
        /// <param name="category">The category, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> for a closing category.</returns>
        public static bool IsDone(StatusCategory category)
        {
            return category is not null && ObjectBoardProjection.CategoryProgress(category) >= 100;
        }

        /// <summary>
        /// Determines whether two occurrences overlap; two that only touch do not.
        /// </summary>
        /// <param name="a">The one occurrence.</param>
        /// <param name="b">The other.</param>
        /// <returns><see langword="true"/> when they overlap.</returns>
        private static bool Overlap(AppointmentOccurrence a, AppointmentOccurrence b)
        {
            var aEnd = a.End == a.Start ? a.End.AddTicks(1) : a.End;
            var bEnd = b.End == b.Start ? b.End.AddTicks(1) : b.End;

            return a.Start < bEnd && b.Start < aEnd;
        }

        /// <summary>
        /// Answers the name of a resource.
        /// </summary>
        /// <param name="resourceId">The resource object.</param>
        /// <returns>The name.</returns>
        private static string ResourceName(Guid resourceId)
        {
            using (CoreHub.SecurityLevelManager?.BeginUnrestricted())
            {
                var entity = CoreHub.ObjectManager.GetObject(resourceId);

                return entity is null ? resourceId.ToString() : (string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : entity.Summary);
            }
        }

        /// <summary>
        /// Answers the active fields of a class.
        /// </summary>
        /// <param name="classId">The class.</param>
        /// <returns>The fields.</returns>
        private static IEnumerable<Field> ClassFieldsOf(Guid classId)
        {
            return CoreHub.FieldManager
                .GetFields(new ClassIdParameter(classId))
                .Where(x => !x.Deprecated && x.State == FieldState.Active);
        }

        /// <summary>
        /// Gives an appointment created without schedule - through the object wizard, an
        /// automation or a portal request - one: the next full hour, for an hour, organized by
        /// whoever created it.
        /// </summary>
        /// <param name="entity">The object added.</param>
        private void OnObjectAdded(ObjectEntity entity)
        {
            if (entity is null || !string.Equals(entity.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (ModelHub.GetAppointment(entity.Id) is not null)
            {
                return;
            }

            var organizer = entity.CreatorId is Guid creator && creator != Guid.Empty ? creator : (Guid?)null;
            var zone = GetDisplayZone(organizer ?? Guid.Empty);
            var local = AppointmentTime.ToLocal(DateTime.UtcNow, zone);
            var nextHour = local.Date.AddHours(local.Hour + 1);

            var schedule = new AppointmentEntity
            {
                ObjectId = entity.Id,
                Start = AppointmentTime.ToUtc(nextHour, zone),
                End = AppointmentTime.ToUtc(nextHour.AddHours(1), zone),
                TimeZone = zone.Id,
                OrganizerId = organizer,
                AutoClose = true
            };

            SaveSchedule(schedule, organizer ?? Guid.Empty);

            if (organizer is not null)
            {
                SetParticipants(entity.Id,
                [
                    new AppointmentParticipant
                    {
                        IdentityId = organizer,
                        Name = CoreHub.IdentityManager?.GetIdentity(organizer.Value)?.Name,
                        Role = AppointmentParticipantRole.Chair,
                        Response = AppointmentResponse.Accepted,
                        Responded = DateTime.UtcNow
                    }
                ]);
            }
        }

        /// <summary>
        /// Drops the bookings of a resource that was removed. The schedule, participants and
        /// bookings of a removed appointment go with it through their foreign keys.
        /// </summary>
        /// <param name="entity">The object removed.</param>
        private static void OnObjectRemoved(ObjectEntity entity)
        {
            if (entity is null || string.Equals(entity.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ModelHub.RemoveAppointmentResourceBookings(entity.Id);
        }

        /// <summary>
        /// Records the changes of the plugin in the audit log - what the core's
        /// <c>AuditManager.Connect</c> does for its own managers, here because the core cannot
        /// name this one.
        /// </summary>
        private void ConnectAudit()
        {
            void Record(AuditCategory category, AuditAction action, object entity, AuditSeverity severity)
            {
                Capture(() => CoreHub.AuditManager?.RecordChange(category, action, entity, severity));
            }

            ScheduleChanged += (_, x) => Record(AuditCategory.Content, AuditAction.Updated, x, AuditSeverity.Info);
            ParticipantResponded += (_, x) => Record(AuditCategory.Content, AuditAction.Updated, x, AuditSeverity.Info);
            AppointmentClosed += (_, x) => Record(AuditCategory.Workflow, AuditAction.Updated, x, AuditSeverity.Info);
            SourceAdded += (_, x) => Record(AuditCategory.Configuration, AuditAction.Created, x, AuditSeverity.Notice);
            SourceUpdated += (_, x) => Record(AuditCategory.Configuration, AuditAction.Updated, x, AuditSeverity.Notice);
            SourceRemoved += (_, x) => Record(AuditCategory.Configuration, AuditAction.Deleted, x, AuditSeverity.Notice);
        }

        /// <summary>
        /// Refuses a layer that is not valid.
        /// </summary>
        /// <param name="source">The layer.</param>
        private void Require(AppointmentCalendarSource source)
        {
            var problems = Validate(source);

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", problems));
            }
        }

        /// <summary>
        /// Clears what a layer of the other kind would carry.
        /// </summary>
        /// <param name="source">The layer.</param>
        private static void Clean(AppointmentCalendarSource source)
        {
            source.Name = source.Name?.Trim();

            if (source.Kind == AppointmentCalendarSource.WorkspaceKind)
            {
                source.Query = null;
            }
            else
            {
                source.SourceWorkspaceId = null;
                source.Query = source.Query?.Trim();
            }
        }

        /// <summary>
        /// Releases nothing: the manager holds no resources of its own; the subscriptions live as
        /// long as the core's managers do.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Runs a reaction without letting a failure reach the write that raised the event.
        /// </summary>
        /// <param name="action">The reaction.</param>
        private void Capture(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }
    }
}
