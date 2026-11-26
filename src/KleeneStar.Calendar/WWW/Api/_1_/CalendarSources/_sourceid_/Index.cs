using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebParameter;
using KleeneStar.Calendar.WebRestApi;
using KleeneStar.Core;
using KleeneStar.Core.WebParameter;
using System;
using System.Collections.Generic;
using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebRestApi;

namespace KleeneStar.Calendar.WWW.Api._1_.CalendarSources._sourceid_
{
    /// <summary>
    /// The endpoint of the dialog removing a layer from a workspace calendar: a GET names the
    /// layer the dialog asks about, a POST or DELETE removes it and answers the calendar.
    /// </summary>
    [Title("kleenestar.calendar:source.delete.title")]
    [SegmentGuid<SourceIdParameter>]
    [Cache]
    public sealed class Index : IRestApi
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Answers the layer.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The layer.</returns>
        [Method(RequestMethod.GET)]
        public IResponse Retrieve(IRequest request)
        {
            var source = Resolve(request, out var workspace);

            if (source is null || !AppointmentAuthorization.MayRead(workspace, request))
            {
                return new ResponseNotFound();
            }

            return FormAnswer.Json(new Dictionary<string, object>
            {
                ["data"] = new Dictionary<string, object> { ["Name"] = source.Name },
                ["prolog"] = I18N.Translate(request, "kleenestar.calendar:source.delete.prolog", source.Name)
            });
        }

        /// <summary>
        /// Removes the layer (the dialog posts).
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The calendar to follow.</returns>
        [Method(RequestMethod.POST)]
        public IResponse Post(IRequest request)
        {
            return Remove(request);
        }

        /// <summary>
        /// Removes the layer.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The calendar to follow.</returns>
        [Method(RequestMethod.DELETE)]
        public IResponse Delete(IRequest request)
        {
            return Remove(request);
        }

        /// <summary>
        /// Removes the layer the route names.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The calendar to follow.</returns>
        private static IResponse Remove(IRequest request)
        {
            var source = Resolve(request, out var workspace);

            if (source is null)
            {
                return new ResponseNotFound();
            }

            if (!AppointmentAuthorization.MayArrange(workspace, request))
            {
                return new ResponseForbidden();
            }

            CalendarHub.AppointmentManager.RemoveSource(source.Id);
            CoreHub.AddNotification(I18N.Translate(request, "kleenestar.calendar:kind.label"), I18N.Translate(request, "kleenestar.calendar:source.delete.done", source.Name));

            return FormAnswer.Success
            (
                CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>()?
                    .BindParameters(new WorkspaceKeyParameter(workspace?.Key))?
                    .ToString()
            );
        }

        /// <summary>
        /// Resolves the layer of the route and its workspace.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="workspace">The workspace.</param>
        /// <returns>The layer, or <see langword="null"/>.</returns>
        private static Model.Entities.AppointmentCalendarSource Resolve(IRequest request, out Model.Entities.Workspace workspace)
        {
            var source = Guid.TryParse(request?.GetParameter<SourceIdParameter>()?.Value, out var id)
                ? CalendarHub.AppointmentManager.GetSource(id)
                : null;

            workspace = source is null ? null : CoreHub.WorkspaceManager.GetWorkspace(source.WorkspaceId);

            return workspace is null ? null : source;
        }
    }
}
