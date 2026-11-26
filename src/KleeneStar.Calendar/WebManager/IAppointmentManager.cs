using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Calendar.WebManager
{
    /// <summary>
    /// Manages what the appointment kind adds to an object: its schedule, its participants and
    /// their answers, the resources it books, the further layers of a workspace calendar, and
    /// the questions asked of all of them - what a calendar shows for a period, what comes up
    /// next, what collides with a planned booking, and what is over and can be closed.
    /// </summary>
    /// <remarks>
    /// The appointment object itself is the core's: it is created, changed and removed through
    /// the object manager. Every read of objects here goes through that manager too, so a
    /// calendar shows each reader only what that reader may see.
    /// </remarks>
    public interface IAppointmentManager : IComponentManager
    {
        /// <summary>
        /// Raised after the schedule of an appointment was written.
        /// </summary>
        event EventHandler<Model.Entities.Appointment> ScheduleChanged;

        /// <summary>
        /// Raised after the participants of an appointment were replaced.
        /// </summary>
        event EventHandler<Guid> ParticipantsChanged;

        /// <summary>
        /// Raised after a participant answered.
        /// </summary>
        event EventHandler<AppointmentParticipant> ParticipantResponded;

        /// <summary>
        /// Raised after a layer was added to a workspace calendar.
        /// </summary>
        event EventHandler<AppointmentCalendarSource> SourceAdded;

        /// <summary>
        /// Raised after a layer of a workspace calendar was changed.
        /// </summary>
        event EventHandler<AppointmentCalendarSource> SourceUpdated;

        /// <summary>
        /// Raised after a layer was removed from a workspace calendar.
        /// </summary>
        event EventHandler<AppointmentCalendarSource> SourceRemoved;

        /// <summary>
        /// Raised after an appointment was closed because its time was over.
        /// </summary>
        event EventHandler<Model.Entities.Appointment> AppointmentClosed;

        /// <summary>
        /// Subscribes to the core's object events and connects the audit log. Idempotent.
        /// </summary>
        void Connect();

        /// <summary>
        /// Returns the zone the times of a request are shown and entered in: the account's, the
        /// plugin's or the host's.
        /// </summary>
        /// <param name="identityId">The account, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The zone.</returns>
        TimeZoneInfo GetDisplayZone(Guid identityId);

        /// <summary>
        /// Returns the schedule of an appointment.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <returns>The schedule, or <see langword="null"/> for an object not scheduled yet.</returns>
        Model.Entities.Appointment GetSchedule(Guid objectId);

        /// <summary>
        /// Returns the schedules of several appointments in one read.
        /// </summary>
        /// <param name="objectIds">The appointment objects.</param>
        /// <returns>The schedules, keyed by object.</returns>
        IReadOnlyDictionary<Guid, Model.Entities.Appointment> GetSchedules(IEnumerable<Guid> objectIds);

        /// <summary>
        /// Returns the schedule a workspace holds for an event of an external calendar.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <param name="externalId">The identifier of the event in the external calendar.</param>
        /// <returns>The schedule, or <see langword="null"/>.</returns>
        Model.Entities.Appointment FindByExternalId(Guid workspaceId, string externalId);

        /// <summary>
        /// Writes the schedule of an appointment. The derived columns are brought in step; when
        /// the time moved, the answers given so far are reset, except the one of the actor.
        /// </summary>
        /// <param name="schedule">The schedule.</param>
        /// <param name="actorId">Who changed it, or <see cref="Guid.Empty"/>.</param>
        /// <returns>The schedule as stored.</returns>
        Model.Entities.Appointment SaveSchedule(Model.Entities.Appointment schedule, Guid actorId);

        /// <summary>
        /// Removes one occurrence from a series.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <param name="occurrenceStart">The start of the occurrence in UTC.</param>
        /// <returns><see langword="true"/> when the occurrence was removed.</returns>
        bool ExcludeOccurrence(Guid objectId, DateTime occurrenceStart);

        /// <summary>
        /// Returns the participants of an appointment.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <returns>The participants.</returns>
        IReadOnlyList<AppointmentParticipant> GetParticipants(Guid objectId);

        /// <summary>
        /// Replaces the participants of an appointment; a participant invited before keeps the
        /// answer they gave. An account invited for the first time watches the appointment.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <param name="participants">The participants.</param>
        /// <returns>The participants new to the appointment.</returns>
        IReadOnlyList<AppointmentParticipant> SetParticipants(Guid objectId, IEnumerable<AppointmentParticipant> participants);

        /// <summary>
        /// Writes the answer of an invited account.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <param name="identityId">The account.</param>
        /// <param name="response">The answer.</param>
        /// <param name="comment">The reservation or reason, or <see langword="null"/>.</param>
        /// <returns>The participant, or <see langword="null"/> when the account is not invited.</returns>
        AppointmentParticipant Respond(Guid objectId, Guid identityId, AppointmentResponse response, string comment);

        /// <summary>
        /// Returns the resources an appointment books.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <returns>The resource objects.</returns>
        IReadOnlyList<Guid> GetResources(Guid objectId);

        /// <summary>
        /// Replaces the resources an appointment books.
        /// </summary>
        /// <param name="objectId">The appointment object.</param>
        /// <param name="resourceIds">The resource objects.</param>
        void SetResources(Guid objectId, IEnumerable<Guid> resourceIds);

        /// <summary>
        /// Returns the further layers of the calendar of a workspace.
        /// </summary>
        /// <param name="workspaceId">The workspace.</param>
        /// <returns>The layers, in their order.</returns>
        IReadOnlyList<AppointmentCalendarSource> GetSources(Guid workspaceId);

        /// <summary>
        /// Returns a layer of a workspace calendar.
        /// </summary>
        /// <param name="sourceId">The layer.</param>
        /// <returns>The layer, or <see langword="null"/>.</returns>
        AppointmentCalendarSource GetSource(Guid sourceId);

        /// <summary>
        /// Checks a layer before it is written.
        /// </summary>
        /// <param name="source">The layer.</param>
        /// <returns>The internationalization keys of what is wrong; empty when it is valid.</returns>
        IReadOnlyList<string> Validate(AppointmentCalendarSource source);

        /// <summary>
        /// Adds a layer to a workspace calendar.
        /// </summary>
        /// <param name="source">The layer.</param>
        /// <returns>The manager.</returns>
        IAppointmentManager AddSource(AppointmentCalendarSource source);

        /// <summary>
        /// Writes the changes of a layer.
        /// </summary>
        /// <param name="source">The layer.</param>
        /// <returns>The manager.</returns>
        IAppointmentManager UpdateSource(AppointmentCalendarSource source);

        /// <summary>
        /// Removes a layer.
        /// </summary>
        /// <param name="sourceId">The layer.</param>
        /// <returns>The manager.</returns>
        IAppointmentManager RemoveSource(Guid sourceId);

        /// <summary>
        /// Returns what the calendar of a workspace shows for a period: the occurrences of its
        /// own appointments and, when asked for, those of its layers, as far as the caller may
        /// see them.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="from">The start of the period (UTC).</param>
        /// <param name="to">The end of the period (UTC).</param>
        /// <param name="layers">Whether the further layers are included.</param>
        /// <returns>The entries, ordered by start.</returns>
        IReadOnlyList<CalendarEntry> GetEntries(Workspace workspace, DateTime from, DateTime to, bool layers = true);

        /// <summary>
        /// Returns the next entries of the calendar of a workspace that are neither over nor
        /// closed.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="now">The present (UTC).</param>
        /// <param name="horizon">How far ahead to look.</param>
        /// <param name="limit">The most entries answered.</param>
        /// <returns>The entries, the soonest first.</returns>
        IReadOnlyList<CalendarEntry> GetUpcoming(Workspace workspace, DateTime now, TimeSpan horizon, int limit);

        /// <summary>
        /// Returns what a planned booking collides with: the appointments its participants
        /// already attend and the bookings of its resources at the same time.
        /// </summary>
        /// <param name="query">The planned booking.</param>
        /// <returns>The conflicts, ordered by start.</returns>
        IReadOnlyList<AppointmentConflict> FindConflicts(AppointmentConflictQuery query);

        /// <summary>
        /// Moves the appointments whose last occurrence is over into a closing state of their
        /// workflow.
        /// </summary>
        /// <param name="now">The present (UTC).</param>
        /// <returns>The number of appointments closed.</returns>
        int CloseExpired(DateTime now);
    }
}
