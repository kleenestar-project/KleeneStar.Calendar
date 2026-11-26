using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// What the pages, fragments and endpoints of the plugin read from a request: the workspace
    /// and the appointment it addresses, the zone its times are shown in, and the appointment
    /// classes it can plan in.
    /// </summary>
    public static class CalendarRequest
    {
        /// <summary>
        /// Resolves the workspace a request addresses: by the workspace key of the route, by the
        /// <c>workspace</c> query parameter, or through the object the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The workspace, or <see langword="null"/>.</returns>
        public static Workspace Workspace(IRequest request)
        {
            var key = request?.GetParameter<WorkspaceKeyParameter>()?.Value;

            if (string.IsNullOrWhiteSpace(key))
            {
                key = request?.GetParameter("workspace")?.Value;
            }

            if (!string.IsNullOrWhiteSpace(key))
            {
                var workspace = CoreHub.WorkspaceManager.GetWorkspaceByKey(key);

                if (workspace is not null)
                {
                    return workspace;
                }

                if (Guid.TryParse(key, out var workspaceId))
                {
                    return CoreHub.WorkspaceManager.GetWorkspace(workspaceId);
                }
            }

            return Appointment(request)?.Workspace ?? AnyObject(request)?.Workspace;
        }

        /// <summary>
        /// Resolves the appointment the route of a request names, read through the object
        /// manager.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The appointment, or <see langword="null"/> when there is none, it is of
        /// another kind, or the caller may not see it.</returns>
        public static ObjectEntity Appointment(IRequest request)
        {
            var entity = AnyObject(request);

            return entity is not null && string.Equals(entity.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase)
                ? entity
                : null;
        }

        /// <summary>
        /// Resolves the object the route of a request names, of whatever kind.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The object, or <see langword="null"/>.</returns>
        public static ObjectEntity AnyObject(IRequest request)
        {
            var key = request?.GetParameter<ObjectKeyParameter>()?.Value;

            return string.IsNullOrWhiteSpace(key) ? null : CoreHub.ObjectManager.GetObjectByKey(key);
        }

        /// <summary>
        /// Returns the account acting in a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The account, or <see cref="Guid.Empty"/> for nobody.</returns>
        public static Guid Identity(IRequest request)
        {
            return CoreHub.SessionManager?.GetCurrentIdentityId(request) ?? Guid.Empty;
        }

        /// <summary>
        /// Returns the zone the times of a request are shown and entered in.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The zone.</returns>
        public static TimeZoneInfo Zone(IRequest request)
        {
            return CalendarHub.AppointmentManager?.GetDisplayZone(Identity(request))
                ?? AppointmentTime.Zone(CalendarHub.Settings.TimeZone);
        }

        /// <summary>
        /// Returns the culture of a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The culture.</returns>
        public static CultureInfo Culture(IRequest request)
        {
            return request?.Culture ?? CultureInfo.CurrentCulture;
        }

        /// <summary>
        /// Returns the active, concrete appointment classes of a workspace, by name.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <returns>The classes.</returns>
        public static IReadOnlyList<Class> Classes(Workspace workspace)
        {
            if (workspace is null)
            {
                return [];
            }

            return [.. CoreHub.ClassManager
                .GetClasses(new Query<Class>().WhereEquals(x => x.WorkspaceId, workspace.Id))
                .Where(x => x.State == ClassState.Active && !x.IsAbstract && !x.IsBlueprint)
                .Where(x => string.Equals(x.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Returns the active templates of some appointment classes: those a class owns and those
        /// that serve it besides their own.
        /// </summary>
        /// <param name="classes">The classes.</param>
        /// <returns>The templates, by name.</returns>
        public static IReadOnlyList<Template> Templates(IEnumerable<Class> classes)
        {
            var result = new Dictionary<Guid, Template>();

            foreach (var @class in classes ?? [])
            {
                foreach (var template in CoreHub.TemplateManager.GetTemplates(new Query<Template>().WhereEquals(x => x.ClassId, @class.Id)))
                {
                    result[template.Id] = template;
                }

                foreach (var templateId in CoreHub.TemplateManager.GetFurtherTemplateIds(@class.Id))
                {
                    if (!result.ContainsKey(templateId) && CoreHub.TemplateManager.GetTemplate(templateId) is { } template)
                    {
                        result[template.Id] = template;
                    }
                }
            }

            return [.. result.Values
                .Where(x => x.State == TemplateState.Active)
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)];
        }

        /// <summary>
        /// Writes the time of an occurrence in the zone and culture of a request, for instance
        /// <c>Wed, 11/25/2026 2:30 PM – 3:30 PM</c> or, for whole days, <c>11/25/2026 – 11/27/2026</c>.
        /// </summary>
        /// <param name="start">The start (UTC).</param>
        /// <param name="end">The end (UTC).</param>
        /// <param name="allDay">Whether the occurrence covers whole days.</param>
        /// <param name="zone">The zone.</param>
        /// <param name="culture">The culture.</param>
        /// <returns>The text.</returns>
        public static string FormatSpan(DateTime start, DateTime end, bool allDay, TimeZoneInfo zone, CultureInfo culture)
        {
            culture ??= CultureInfo.CurrentCulture;

            var localStart = AppointmentTime.ToLocal(start, zone);
            var localEnd = AppointmentTime.ToLocal(end, zone);
            var day = culture.DateTimeFormat.AbbreviatedDayNames[(int)localStart.DayOfWeek];
            var date = culture.DateTimeFormat.ShortDatePattern;
            var time = culture.DateTimeFormat.ShortTimePattern;

            if (allDay)
            {
                var lastDay = localEnd > localStart ? localEnd.AddTicks(-1).Date : localStart.Date;

                return lastDay <= localStart.Date
                    ? $"{day}, {localStart.ToString(date, culture)}"
                    : $"{day}, {localStart.ToString(date, culture)} – {lastDay.ToString(date, culture)}";
            }

            return localEnd.Date == localStart.Date
                ? $"{day}, {localStart.ToString(date, culture)} {localStart.ToString(time, culture)} – {localEnd.ToString(time, culture)}"
                : $"{day}, {localStart.ToString(date, culture)} {localStart.ToString(time, culture)} – {localEnd.ToString(date, culture)} {localEnd.ToString(time, culture)}";
        }

        /// <summary>
        /// Writes the time of an occurrence in the zone and culture of a request.
        /// </summary>
        /// <param name="start">The start (UTC).</param>
        /// <param name="end">The end (UTC).</param>
        /// <param name="allDay">Whether the occurrence covers whole days.</param>
        /// <param name="request">The request.</param>
        /// <returns>The text.</returns>
        public static string FormatSpan(DateTime start, DateTime end, bool allDay, IRequest request)
        {
            return FormatSpan(start, end, allDay, Zone(request), Culture(request));
        }
    }
}
