using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using KleeneStar.Core.WebParameter;
using KleeneStar.Core.WebScope;
using KleeneStar.Core.WebUri;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebCore.WebPage;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WWW.Appointment._objectkey_
{
    /// <summary>
    /// The page of a single appointment (<c>/appointment/{objectkey}</c>). As an
    /// <see cref="IScopeObjectKindDetail"/> it carries the core's full object detail experience -
    /// headline, description, relations, attachments, property cards, comments, the dialogs of
    /// the overflow menu and the shared sidebar - and the plugin adds the schedule, the
    /// participants with their answers, the booked resources and the conflicts.
    /// </summary>
    /// <remarks>
    /// An object of another kind is redirected to the detail view of its own kind, and a former
    /// key to the current one, as the core's detail pages do.
    /// </remarks>
    [WebIcon<IconCalendarDays>]
    [ObjectKeySegment]
    [Scope<IScopeGeneral>]
    [Scope<IScopeObjectKindDetail>]
    [Cache]
    public sealed class Index : IPage<VisualTreeWebApp>, IScopeGeneral, IScopeObjectKindDetail
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Index()
        {
        }

        /// <summary>
        /// Processing of the resource.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;
            var key = request.GetParameter<ObjectKeyParameter>()?.Value;
            var @object = CalendarRequest.AnyObject(request);

            if (@object is not null &&
                (!string.Equals(@object.Kind, Model.Entities.ObjectKind.Appointment, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(@object.Key, key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new RedirectException(CoreHub.ObjectKindManager.ResolveDetailUri(@object));
            }

            if (@object is not null)
            {
                var identityId = CalendarRequest.Identity(request);
                CoreHub.ObjectManager.RecordVisit(identityId, @object.Id);
                CoreHub.WorkspaceManager.RecordVisit(identityId, @object.WorkspaceId);
            }

            visualTree.BreadcrumbUri = renderContext.PageContext.ApplicationContext.Route
                .Concat(new WorkspaceKeyUriPathSegmentVariable<WorkspaceKeyParameter>()
                {
                    Value = @object?.Workspace?.Key,
                    Uri = CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>()
                        .BindParameters(new WorkspaceKeyParameter(@object?.Workspace?.Key))
                })
                .Concat(new ObjectKeyUriPathSegmentVariable<ObjectKeyParameter>()
                {
                    Value = @object?.Key,
                    Uri = CoreHub.GetUri<Index>()
                        .BindParameters(new ObjectKeyParameter(@object?.Key))
                })
                .ToUri()
                .BindParameters(request);

            visualTree.Title = @object?.Summary;
            visualTree.Content.MainPanel.Headline.Title = @object?.Summary;
        }
    }
}
