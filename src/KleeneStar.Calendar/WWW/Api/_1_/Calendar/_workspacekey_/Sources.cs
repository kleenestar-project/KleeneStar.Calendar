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
    /// The endpoint of the dialog including a layer in the calendar of a workspace: another
    /// workspace (its appointments) or a WQL query (the objects it selects). A GET answers the
    /// starting values and the layers included so far (<c>sources</c>, for an API client); a
    /// POST includes the layer and answers the calendar, which the dialog follows.
    /// </summary>
    [Title("kleenestar.calendar:source.add.title")]
    [Cache]
    public sealed class Sources : IRestApi
    {
        /// <summary>Input name of the name of the layer.</summary>
        public const string NameField = "Name";

        /// <summary>Input name of the kind of the layer.</summary>
        public const string KindField = "Kind";

        /// <summary>Input name of the workspace shown.</summary>
        public const string WorkspaceField = "SourceWorkspaceId";

        /// <summary>Input name of the query.</summary>
        public const string QueryField = "Query";

        /// <summary>Input name of the colour.</summary>
        public const string ColorField = "Color";

        /// <summary>
        /// The colours offered for a layer, after the palette of the decks: teal, orange, and
        /// the colours of the core's badges.
        /// </summary>
        public static IReadOnlyList<string> Colors { get; } = ["#2ec4b6", "#ff6b00", "#6f42c1", "#0d6efd", "#198754", "#d63384", "#6c757d"];

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Sources()
        {
        }

        /// <summary>
        /// Answers the starting values and the layers included so far.
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

            var sources = CalendarHub.AppointmentManager.GetSources(workspace.Id);

            return FormAnswer.Json(new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object>
                {
                    [NameField] = string.Empty,
                    [KindField] = AppointmentCalendarSource.WorkspaceKind,
                    [WorkspaceField] = string.Empty,
                    [QueryField] = string.Empty,
                    [ColorField] = Colors[sources.Count % Colors.Count]
                },
                ["sources"] = sources.Select(x => new
                {
                    id = x.Id,
                    name = x.Name,
                    kind = x.Kind,
                    workspace = x.SourceWorkspaceId,
                    query = x.Query,
                    color = x.Color,
                    enabled = x.Enabled
                })
            });
        }

        /// <summary>
        /// Includes the layer.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The calendar to follow, or the refusals.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Create(IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);

            if (workspace is null)
            {
                return new ResponseNotFound();
            }

            if (!AppointmentAuthorization.MayArrange(workspace, request))
            {
                return new ResponseForbidden();
            }

            var payload = FormPayload.Read(request);
            var kind = (payload.Text(KindField) ?? AppointmentCalendarSource.WorkspaceKind).ToLowerInvariant();
            var color = payload.Text(ColorField);

            var source = new AppointmentCalendarSource
            {
                WorkspaceId = workspace.Id,
                Name = payload.Text(NameField),
                Kind = kind,
                SourceWorkspaceId = payload.Id(WorkspaceField),
                Query = payload.Text(QueryField),
                Color = color is not null && System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{3,8}$") ? color : Colors[0],
                Enabled = true,
                CreatedById = CalendarRequest.Identity(request) is var id && id != Guid.Empty ? id : null
            };

            // a workspace layer without a name is named after the workspace
            if (string.IsNullOrWhiteSpace(source.Name) && kind == AppointmentCalendarSource.WorkspaceKind && source.SourceWorkspaceId is Guid sourceId)
            {
                source.Name = CoreHub.WorkspaceManager.GetWorkspace(sourceId)?.Name;
            }

            // a workspace the caller may not read is not one to include
            if (kind == AppointmentCalendarSource.WorkspaceKind && source.SourceWorkspaceId is Guid included
                && !AppointmentAuthorization.MayRead(CoreHub.WorkspaceManager.GetWorkspace(included), request))
            {
                source.SourceWorkspaceId = null;
            }

            var problems = CalendarHub.AppointmentManager.Validate(source);

            if (problems.Count > 0)
            {
                var errors = new Dictionary<string, string>();

                foreach (var problem in problems)
                {
                    var field = problem switch
                    {
                        "kleenestar.calendar:source.name.required" => NameField,
                        "kleenestar.calendar:source.query.required" or "kleenestar.calendar:source.query.invalid" => QueryField,
                        "kleenestar.calendar:source.workspace.required" or "kleenestar.calendar:source.workspace.self" => WorkspaceField,
                        _ => KindField
                    };

                    errors.TryAdd(field, problem);
                }

                return FormAnswer.Errors(request, errors);
            }

            CalendarHub.AppointmentManager.AddSource(source);
            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:source.add.done", source.Name));

            return FormAnswer.Success(CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>()?.BindParameters(request)?.ToString());
        }
    }
}
