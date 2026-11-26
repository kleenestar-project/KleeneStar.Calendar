using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;
using WebExpress.WebCore.WebStatusPage;
using AppointmentEntity = KleeneStar.Model.Entities.Appointment;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// The calendar endpoint of a workspace: the occurrences of its appointments and of its
    /// layers for the period the schedule control asks for, a drag that moves an appointment,
    /// and a span drawn on the grid that plans a new one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control speaks local wall-clock time without zone (<c>2026-11-25T14:30:00</c>) and
    /// gives whole days as dates whose end is the last day; the endpoint converts in the zone of
    /// the caller (<see cref="CalendarRequest.Zone"/>), so a reader in another zone sees the same
    /// appointment at their own time.
    /// </para>
    /// <para>
    /// A move is refused with 409 for an occurrence of a series (it would move the whole series;
    /// the dialog says which) and for an entry of a layer (it belongs to another calendar);
    /// moving is changing the object and needs the right to.
    /// </para>
    /// </remarks>
    [WorkspaceKeySegment]
    [Title("kleenestar.calendar:kind.label")]
    [Cache]
    public sealed class Index : RestApiSchedule
    {
        /// <summary>
        /// The serializer profile of the entry wire shape.
        /// </summary>
        private static readonly JsonSerializerOptions _wireOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Returns the entries overlapping the requested period.
        /// </summary>
        /// <param name="from">The first day of the period, or <see langword="null"/>.</param>
        /// <param name="to">The day after the period, or <see langword="null"/>.</param>
        /// <param name="request">The incoming request.</param>
        /// <returns>The entries.</returns>
        protected override IEnumerable<RestApiScheduleItem> RetrieveItems(DateTime? from, DateTime? to, IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return [];
            }

            var zone = CalendarRequest.Zone(request);
            var localFrom = (from ?? AppointmentTime.ToLocal(DateTime.UtcNow, zone).Date.AddDays(-7)).Date;
            var localTo = (to ?? localFrom.AddDays(42)).Date;

            if (localTo <= localFrom)
            {
                localTo = localFrom.AddDays(1);
            }

            var entries = CalendarHub.AppointmentManager.GetEntries
            (
                workspace,
                AppointmentTime.ToUtc(localFrom, zone),
                AppointmentTime.ToUtc(localTo.AddDays(1), zone)
            );

            return [.. entries.Select(x => ToItem(x, zone, request))];
        }

        /// <summary>
        /// Handles the PUT that persists a moved entry.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns>
        /// <c>200</c> with the stored entry, <c>404</c> when the id names no appointment of the
        /// calendar, <c>403</c> without the right to change it, <c>409</c> for an occurrence of a
        /// series or an entry of a layer, <c>400</c> for a malformed payload.
        /// </returns>
        [Method(RequestMethod.PUT)]
        public override IResponse Update(IRequest request)
        {
            var item = ReadItem(request);

            if (item is null || string.IsNullOrWhiteSpace(item.Id))
            {
                return new ResponseBadRequest(new StatusMessage("Missing or malformed request body."));
            }

            try
            {
                if (item.Id.Contains('@'))
                {
                    return Conflict(request, "kleenestar.calendar:calendar.move.series");
                }

                if (!Guid.TryParse(item.Id, out var objectId))
                {
                    return new ResponseNotFound(new StatusMessage($"No item found for id '{item.Id}'."));
                }

                var workspace = CalendarRequest.Workspace(request);
                var entity = CoreHub.ObjectManager.GetObject(objectId);

                if (entity is null || workspace is null)
                {
                    return new ResponseNotFound(new StatusMessage($"No item found for id '{item.Id}'."));
                }

                if (entity.WorkspaceId != workspace.Id || !string.Equals(entity.Kind, ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(request, "kleenestar.calendar:calendar.move.layer");
                }

                if (!AppointmentAuthorization.MayWrite(entity, request))
                {
                    return new ResponseForbidden();
                }

                var schedule = CalendarHub.AppointmentManager.GetSchedule(entity.Id);

                if (schedule is null)
                {
                    return new ResponseNotFound(new StatusMessage($"No item found for id '{item.Id}'."));
                }

                if (!string.IsNullOrWhiteSpace(schedule.Recurrence))
                {
                    return Conflict(request, "kleenestar.calendar:calendar.move.series");
                }

                var zone = CalendarRequest.Zone(request);

                if (!TryReadSpan(item, zone, schedule, out var start, out var end, out var allDay))
                {
                    return new ResponseBadRequest(new StatusMessage("Missing or malformed start or end."));
                }

                schedule.Start = start;
                schedule.End = end;
                schedule.AllDay = allDay;

                CalendarHub.AppointmentManager.SaveSchedule(schedule, CalendarRequest.Identity(request));

                return Json(new { success = true, item });
            }
            catch (Exception ex)
            {
                return RestApiFault.BadRequest(request, ex, "error processing put request.");
            }
        }

        /// <summary>
        /// Handles the POST of a span drawn on the grid: a new appointment in the first
        /// appointment class of the workspace the caller may create in, organized by the caller.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><c>200</c> with the new entry, <c>403</c> without a class to create in,
        /// <c>400</c> for a malformed payload.</returns>
        [Method(RequestMethod.POST)]
        public override IResponse Create(IRequest request)
        {
            var item = ReadItem(request);

            if (item is null)
            {
                return new ResponseBadRequest(new StatusMessage("Missing or malformed request body."));
            }

            try
            {
                var workspace = CalendarRequest.Workspace(request);
                var @class = CalendarRequest.Classes(workspace).FirstOrDefault(x => AppointmentAuthorization.MayCreate(x, request));

                if (@class is null)
                {
                    return new ResponseForbidden();
                }

                var zone = CalendarRequest.Zone(request);
                var identityId = CalendarRequest.Identity(request);

                if (!TryReadSpan(item, zone, null, out var start, out var end, out var allDay))
                {
                    return new ResponseBadRequest(new StatusMessage("Missing or malformed start or end."));
                }

                if (!allDay && end <= start)
                {
                    end = start.AddHours(1);
                }

                var title = string.IsNullOrWhiteSpace(item.Title)
                    ? I18N.Translate(request, "kleenestar.calendar:appointment.new.title")
                    : item.Title.Trim();

                var entity = AppointmentObjects.Create(@class, title, null, identityId);

                var schedule = CalendarHub.AppointmentManager.GetSchedule(entity.Id) ?? new AppointmentEntity { ObjectId = entity.Id };
                schedule.Start = start;
                schedule.End = end;
                schedule.AllDay = allDay;
                schedule.TimeZone = zone.Id;
                schedule.OrganizerId = identityId == Guid.Empty ? null : identityId;

                CalendarHub.AppointmentManager.SaveSchedule(schedule, identityId);

                var entry = CalendarHub.AppointmentManager
                    .GetEntries(workspace, schedule.Start.AddTicks(-1), schedule.End.AddTicks(1), false)
                    .FirstOrDefault(x => x.Object.Id == entity.Id);

                var created = entry is null ? item : ToItem(entry, zone, request);

                return Json(new { success = true, item = created });
            }
            catch (Exception ex)
            {
                return RestApiFault.BadRequest(request, ex, "error processing post request.");
            }
        }

        /// <summary>
        /// Refuses a delete: an appointment is removed through the object flow.
        /// </summary>
        /// <param name="request">The incoming request.</param>
        /// <returns><c>405</c>.</returns>
        [Method(RequestMethod.DELETE)]
        public override IResponse Delete(IRequest request)
        {
            return Conflict(request, "kleenestar.calendar:calendar.delete.refused");
        }

        /// <summary>
        /// Projects an entry onto the wire shape of the schedule control.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <param name="zone">The zone of the caller.</param>
        /// <param name="request">The request.</param>
        /// <returns>The item.</returns>
        public static RestApiScheduleItem ToItem(CalendarEntry entry, TimeZoneInfo zone, IRequest request)
        {
            var entity = entry.Object;
            var localStart = AppointmentTime.ToLocal(entry.Start, zone);
            var localEnd = AppointmentTime.ToLocal(entry.End, zone);

            string start;
            string end;

            if (entry.AllDay)
            {
                // whole days travel as dates, the end being the last day
                var lastDay = localEnd > localStart ? localEnd.AddTicks(-1).Date : localStart.Date;
                start = FormatDate(localStart.Date);
                end = FormatDate(lastDay);
            }
            else
            {
                start = Format(localStart);
                end = Format(localEnd);
            }

            var layer = entry.Source;
            var color = layer?.Color;
            var meta = new Dictionary<string, string>
            {
                ["key"] = entity.Key,
                ["status"] = entry.Category is null ? string.Empty : ObjectBoardProjection.CategoryLabel(entry.Category),
                ["when"] = CalendarRequest.FormatSpan(entry.Start, entry.End, entry.AllDay, zone, CalendarRequest.Culture(request))
            };

            if (!string.IsNullOrWhiteSpace(entry.Location))
            {
                meta["location"] = entry.Location;
            }

            if (layer is not null)
            {
                meta["layer"] = layer.Name;
            }

            if (entry.Recurring)
            {
                meta["series"] = "true";
            }

            return new RestApiScheduleItem
            {
                Id = entry.Id,
                Title = string.IsNullOrWhiteSpace(entity.Summary) ? entity.Key : entity.Summary,
                Start = start,
                End = end,
                AllDay = entry.AllDay,
                Category = layer?.Name ?? entity.Class?.Name ?? CoreHub.ClassManager.GetClass(entity.ClassId)?.Name,
                ColorCss = string.IsNullOrWhiteSpace(color) ? ObjectBoardProjection.CategoryBackgroundCss(entry.Category) : null,
                ColorStyle = string.IsNullOrWhiteSpace(color) ? null : $"background-color:{color};border-color:{color};color:#fff",
                Uri = CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString(),
                Meta = meta
            };
        }

        /// <summary>
        /// Reads the span of a moved or drawn entry.
        /// </summary>
        /// <param name="item">The entry.</param>
        /// <param name="zone">The zone of the caller.</param>
        /// <param name="schedule">The stored schedule, whose length a move without end keeps, or
        /// <see langword="null"/>.</param>
        /// <param name="start">The start (UTC).</param>
        /// <param name="end">The end (UTC).</param>
        /// <param name="allDay">Whether the entry covers whole days.</param>
        /// <returns><see langword="true"/> when a start was read.</returns>
        private static bool TryReadSpan(RestApiScheduleItem item, TimeZoneInfo zone, AppointmentEntity schedule, out DateTime start, out DateTime end, out bool allDay)
        {
            start = default;
            end = default;
            allDay = item.AllDay || IsDate(item.Start);

            if (!TryParseLocal(item.Start, zone, out var localStart))
            {
                return false;
            }

            if (allDay)
            {
                var lastDay = TryParseLocal(item.End, zone, out var localEnd) ? localEnd.Date : localStart.Date;

                if (lastDay < localStart.Date)
                {
                    lastDay = localStart.Date;
                }

                start = AppointmentTime.ToUtc(localStart.Date, zone);
                end = AppointmentTime.ToUtc(lastDay.AddDays(1), zone);

                return true;
            }

            start = AppointmentTime.ToUtc(localStart, zone);

            if (TryParseLocal(item.End, zone, out var localFinish) && localFinish > localStart)
            {
                end = AppointmentTime.ToUtc(localFinish, zone);
            }
            else
            {
                var length = schedule is null || schedule.AllDay ? TimeSpan.FromHours(1) : schedule.End - schedule.Start;
                end = start + length;
            }

            return true;
        }

        /// <summary>
        /// Reads a wall-clock time of the wire shape.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="zone">The zone of the caller, which a time with offset is shown in.</param>
        /// <param name="value">The local time.</param>
        /// <returns><see langword="true"/> when the text was read.</returns>
        private static bool TryParseLocal(string text, TimeZoneInfo zone, out DateTime value)
        {
            value = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset) && HasOffset(text))
            {
                // an explicit offset names an instant; it is shown in local time like any other
                value = AppointmentTime.ToLocal(offset.UtcDateTime, zone);
                return true;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            {
                value = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Determines whether a wire time carries an offset or a <c>Z</c>.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        private static bool HasOffset(string text)
        {
            var time = text.IndexOf('T');

            return time > 0 && (text.EndsWith('Z') || text.IndexOf('+', time) > 0 || text.IndexOf('-', time) > 0);
        }

        /// <summary>
        /// Determines whether a wire time is a date without time.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns><see langword="true"/> for a date.</returns>
        private static bool IsDate(string text)
        {
            return !string.IsNullOrWhiteSpace(text) && !text.Contains('T') && !text.Contains(':');
        }

        /// <summary>
        /// Answers a refusal the calendar shows.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="key">The internationalization key of the message.</param>
        /// <returns><c>409</c>.</returns>
        private static IResponse Conflict(IRequest request, string key)
        {
            return new ResponseConflict(new StatusMessage(I18N.Translate(request, key)));
        }

        /// <summary>
        /// Answers JSON.
        /// </summary>
        /// <param name="payload">The payload.</param>
        /// <returns><c>200</c>.</returns>
        private static IResponse Json(object payload)
        {
            return new ResponseOK
            {
                Content = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, _wireOptions))
            }
                .AddHeaderContentType("application/json");
        }

        /// <summary>
        /// Deserializes the entry carried in the request body.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The entry, or <see langword="null"/>.</returns>
        private static RestApiScheduleItem ReadItem(IRequest request)
        {
            if (request is not Request requestData || requestData.Content is not { Length: > 0 })
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<RestApiScheduleItem>(requestData.Content, _wireOptions);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
