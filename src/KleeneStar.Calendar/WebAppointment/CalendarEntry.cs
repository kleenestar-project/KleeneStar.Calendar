using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// One entry of a calendar: an occurrence of an appointment, or an object of another kind
    /// a query layer places by its date fields.
    /// </summary>
    public sealed class CalendarEntry
    {
        /// <summary>
        /// Gets the object the entry stands for.
        /// </summary>
        public Model.Entities.Object Object { get; init; }

        /// <summary>
        /// Gets the start in UTC.
        /// </summary>
        public DateTime Start { get; init; }

        /// <summary>
        /// Gets the end in UTC.
        /// </summary>
        public DateTime End { get; init; }

        /// <summary>
        /// Gets whether the entry covers whole days.
        /// </summary>
        public bool AllDay { get; init; }

        /// <summary>
        /// Gets whether the entry is an occurrence of a series.
        /// </summary>
        public bool Recurring { get; init; }

        /// <summary>
        /// Gets the place, or <see langword="null"/>.
        /// </summary>
        public string Location { get; init; }

        /// <summary>
        /// Gets the layer the entry comes from, or <see langword="null"/> for the workspace's own
        /// appointments.
        /// </summary>
        public AppointmentCalendarSource Source { get; init; }

        /// <summary>
        /// Gets the status category of the object, or <see langword="null"/>.
        /// </summary>
        public StatusCategory Category { get; init; }

        /// <summary>
        /// Gets whether the entry is an occurrence of an appointment (rather than an object of
        /// another kind placed by its date fields).
        /// </summary>
        public bool IsAppointment { get; init; }

        /// <summary>
        /// Gets the identifier of the entry: the object id for a single appointment or another
        /// object, the object id and the start for an occurrence of a series.
        /// </summary>
        public string Id => Recurring
            ? $"{Object?.Id}@{AppointmentSchedule.FormatInstant(Start)}"
            : Object?.Id.ToString();
    }

    /// <summary>
    /// What a conflict is about.
    /// </summary>
    public enum AppointmentConflictKind
    {
        /// <summary>
        /// A participant attends another appointment at the same time.
        /// </summary>
        Participant,

        /// <summary>
        /// A resource is booked for another appointment at the same time.
        /// </summary>
        Resource
    }

    /// <summary>
    /// A collision of a planned booking with another appointment.
    /// </summary>
    public sealed class AppointmentConflict
    {
        /// <summary>
        /// Gets what the conflict is about.
        /// </summary>
        public AppointmentConflictKind Kind { get; init; }

        /// <summary>
        /// Gets the participant (an account) or the resource (an object) that is taken twice.
        /// </summary>
        public Guid SubjectId { get; init; }

        /// <summary>
        /// Gets the name of the participant or resource.
        /// </summary>
        public string SubjectName { get; init; }

        /// <summary>
        /// Gets the appointment the planned booking collides with.
        /// </summary>
        public Guid OtherObjectId { get; init; }

        /// <summary>
        /// Gets the appointment the planned booking collides with, or <see langword="null"/> when
        /// the caller may not see it - the time is taken, what for is not the caller's to know.
        /// </summary>
        public Model.Entities.Object Other { get; init; }

        /// <summary>
        /// Gets the start of the colliding occurrence in UTC.
        /// </summary>
        public DateTime Start { get; init; }

        /// <summary>
        /// Gets the end of the colliding occurrence in UTC.
        /// </summary>
        public DateTime End { get; init; }

        /// <summary>
        /// Gets whether the colliding occurrence covers whole days.
        /// </summary>
        public bool AllDay { get; init; }
    }

    /// <summary>
    /// A planned booking whose conflicts are asked for.
    /// </summary>
    public sealed class AppointmentConflictQuery
    {
        /// <summary>
        /// Gets the appointment being planned, whose own bookings do not count, or
        /// <see langword="null"/> for a new one.
        /// </summary>
        public Guid? ObjectId { get; init; }

        /// <summary>
        /// Gets the start of the (first) occurrence in UTC.
        /// </summary>
        public DateTime Start { get; init; }

        /// <summary>
        /// Gets the end of the (first) occurrence in UTC.
        /// </summary>
        public DateTime End { get; init; }

        /// <summary>
        /// Gets whether the appointment covers whole days.
        /// </summary>
        public bool AllDay { get; init; }

        /// <summary>
        /// Gets the zone of a series.
        /// </summary>
        public string TimeZone { get; init; }

        /// <summary>
        /// Gets the recurrence rule, or <see langword="null"/>.
        /// </summary>
        public string Recurrence { get; init; }

        /// <summary>
        /// Gets the occurrences removed from the series, as stored.
        /// </summary>
        public string Exceptions { get; init; }

        /// <summary>
        /// Gets the accounts invited.
        /// </summary>
        public IReadOnlyCollection<Guid> IdentityIds { get; init; } = [];

        /// <summary>
        /// Gets the resources booked.
        /// </summary>
        public IReadOnlyCollection<Guid> ResourceIds { get; init; } = [];
    }
}
