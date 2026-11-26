using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using KleeneStar.Model.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// The endpoint of the dialog planning a new appointment in a workspace. A GET answers the
    /// values the dialog starts from - the next full hour, the caller as participant, and, for
    /// <c>?id={template}</c>, what the template presets; a POST plans the appointment and answers
    /// the calendar, which the dialog follows.
    /// </summary>
    [Title("kleenestar.calendar:appointment.add.title")]
    [Cache]
    public sealed class Appointment : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Appointment()
        {
        }

        /// <summary>
        /// Answers the values the dialog starts from.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The values.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var zone = CalendarRequest.Zone(request);
            var culture = CalendarRequest.Culture(request);
            var identityId = CalendarRequest.Identity(request);
            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request)).ToList();
            var template = Template(request, classes);

            var values = AppointmentForm.Values(null, null, [], [], zone, culture);
            values[AppointmentForm.Participants] = identityId == Guid.Empty ? new List<string>() : new List<string> { identityId.ToString() };
            values[AppointmentForm.ClassId] = (template is null ? classes.FirstOrDefault()?.Id : ClassOf(template, classes)?.Id)?.ToString() ?? string.Empty;
            values[AppointmentForm.TemplateId] = template?.Id.ToString() ?? string.Empty;

            if (template is not null)
            {
                AppointmentForm.ApplyPresets(values, CoreHub.TemplateManager.GetPresets(template.Id), zone, culture);
            }

            return FormAnswer.Values(values);
        }

        /// <summary>
        /// Plans the appointment.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The calendar to follow, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Create(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            var payload = FormPayload.Read(request);
            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request)).ToList();
            var classId = payload.Id(AppointmentForm.ClassId);
            var @class = classes.FirstOrDefault(x => x.Id == classId) ?? (classId is null ? classes.FirstOrDefault() : null);

            if (@class is null)
            {
                return FormAnswer.Errors(request, new Dictionary<string, string> { [AppointmentForm.ClassId] = "kleenestar.calendar:appointment.class.required" });
            }

            var keys = new List<string>();
            var draft = AppointmentForm.Read(payload, CalendarRequest.Culture(request), CalendarRequest.Zone(request), keys);

            if (keys.Count > 0)
            {
                return FormAnswer.Errors(request, ErrorsOf(keys));
            }

            var errors = AppointmentPlanning.Check(draft, null, request);

            if (errors.Count > 0)
            {
                return FormAnswer.Errors(request, errors);
            }

            var templateId = payload.Id(AppointmentForm.TemplateId) ?? (Guid.TryParse(request.GetParameter("id")?.Value, out var id) ? id : null);
            var template = templateId is Guid tid ? CalendarRequest.Templates(classes).FirstOrDefault(x => x.Id == tid) : null;
            var presets = template is null ? null : CoreHub.TemplateManager.GetPresets(template.Id);

            var entity = AppointmentPlanning.Create(@class, draft, CalendarRequest.Identity(request), presets);

            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:appointment.add.done", entity.Key, entity.Summary));

            return FormAnswer.Success(CoreHub.ObjectKindManager.ResolveDetailUri(entity)?.ToString(), new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object>
                {
                    ["created"] = true,
                    ["id"] = entity.Id,
                    ["key"] = entity.Key
                }
            });
        }

        /// <summary>
        /// Assigns the messages of the dialog check to the inputs they concern.
        /// </summary>
        /// <param name="keys">The internationalization keys.</param>
        /// <returns>The messages, keyed by input name.</returns>
        public static Dictionary<string, string> ErrorsOf(IEnumerable<string> keys)
        {
            var errors = new Dictionary<string, string>();

            foreach (var key in keys)
            {
                var field = key switch
                {
                    "kleenestar.calendar:appointment.summary.required" => AppointmentForm.Summary,
                    "kleenestar.calendar:appointment.start.required" => AppointmentForm.StartDate,
                    "kleenestar.calendar:appointment.end.before" => AppointmentForm.EndDate,
                    "kleenestar.calendar:appointment.rule.invalid" => AppointmentForm.Rule,
                    _ => AppointmentForm.Summary
                };

                errors.TryAdd(field, key);
            }

            return errors;
        }

        /// <summary>
        /// Resolves the template a request names (<c>?id=</c> or <c>?template=</c>) among the
        /// templates of the classes.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="classes">The classes the caller may plan in.</param>
        /// <returns>The template, or <see langword="null"/>.</returns>
        private static Template Template(IRequest request, IReadOnlyList<Class> classes)
        {
            var text = request.GetParameter("template")?.Value ?? request.GetParameter("id")?.Value;

            return Guid.TryParse(text, out var id)
                ? CalendarRequest.Templates(classes).FirstOrDefault(x => x.Id == id)
                : null;
        }

        /// <summary>
        /// Answers the class a template creates in, among the classes.
        /// </summary>
        /// <param name="template">The template.</param>
        /// <param name="classes">The classes.</param>
        /// <returns>The class, or <see langword="null"/>.</returns>
        private static Class ClassOf(Template template, IReadOnlyList<Class> classes)
        {
            return classes.FirstOrDefault(x => x.Id == template.ClassId)
                ?? classes.FirstOrDefault(x => CoreHub.TemplateManager.GetFurtherTemplateIds(x.Id).Contains(template.Id))
                ?? classes.FirstOrDefault();
        }
    }
}
