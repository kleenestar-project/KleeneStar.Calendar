using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using KleeneStar.Model;
using KleeneStar.Model.Entities;
using System;
using System.Linq;
using WebExpress.WebApp.WebRestApi;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebMessage;
using WebExpress.WebIndex.Queries;
using WebExpress.WebIndex.Wql;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_
{
    /// <summary>
    /// The appointment classes of the workspace the caller may create in: the class selection of
    /// the dialogs planning and importing appointments.
    /// </summary>
    [Title("kleenestar.calendar:appointment.class.label")]
    [Cache]
    public sealed class Classes : RestApiSelection<Class>
    {
        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Class> query, IQueryContext context, IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);
            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request));

            return query.Apply(classes.AsQueryable())
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = x.Name })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Class> Filter(string filter, IQuery<Class> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }

    /// <summary>
    /// The templates of the workspace's appointment classes: the template selection of the
    /// dialog planning an appointment.
    /// </summary>
    [Title("kleenestar.calendar:template.label")]
    [Cache]
    public sealed class Templates : RestApiSelection<Template>
    {
        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Template> query, IQueryContext context, IRequest request)
        {
            var workspace = CalendarRequest.Workspace(request);
            var classes = CalendarRequest.Classes(workspace).Where(x => AppointmentAuthorization.MayCreate(x, request));

            return query.Apply(CalendarRequest.Templates(classes).AsQueryable())
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = x.Name })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Template> Filter(string filter, IQuery<Template> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }

    /// <summary>
    /// The accounts that can be invited: every active account; a tenant member is offered the
    /// colleagues of their own tenant, as the core's selections of people do.
    /// </summary>
    [Title("kleenestar.calendar:participant.label")]
    [Cache]
    public sealed class Participants : RestApiSelection<Identity>
    {
        /// <inheritdoc/>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Identity> query, IQueryContext context, IRequest request)
        {
            var caller = CoreHub.IdentityManager.GetCurrentIdentity(request);

            return CoreHub.IdentityManager
                .GetIdentities(query, context)
                .Where(x => x.State == IdentityState.Active)
                .Where(x => caller?.TenantId is null || x.TenantId == caller.TenantId)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = string.IsNullOrWhiteSpace(x.Position) ? x.Name : $"{x.Name} · {x.Position}"
                })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Identity> Filter(string filter, IQuery<Identity> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }

    /// <summary>
    /// The resources that can be booked: the objects of the asset kind the caller may see, of
    /// every workspace - rooms, projectors and vehicles are the installation's inventory.
    /// </summary>
    [Title("kleenestar.calendar:resource.label")]
    [Cache]
    public sealed class Resources : RestApiSelection<ObjectEntity>
    {
        /// <summary>
        /// The most resources offered at once; the search narrows further.
        /// </summary>
        public const int MaxItems = 200;

        /// <inheritdoc/>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<ObjectEntity> query, IQueryContext context, IRequest request)
        {
            return CoreHub.ObjectManager
                .GetObjects(query.WhereEquals(x => x.Kind, ObjectKind.Asset).WithPaging(0, MaxItems), context)
                .Where(x => x.State == WorkspaceState.Active)
                .OrderBy(x => x.Summary)
                .Select(x => new RestApiSelectionItem
                {
                    Id = x.Id,
                    Text = string.IsNullOrWhiteSpace(x.Summary) ? x.Key : $"{x.Summary} · {x.Key}"
                })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<ObjectEntity> Filter(string filter, IQuery<ObjectEntity> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Summary, filter);
        }
    }

    /// <summary>
    /// The workspaces whose calendar can be included: every other workspace the caller may read.
    /// </summary>
    [Title("kleenestar.calendar:source.workspace.label")]
    [Cache]
    public sealed class Workspaces : RestApiSelection<Workspace>
    {
        /// <inheritdoc/>
        protected override IQueryContext CreateContext()
        {
            return ModelHub.CreateDbContext();
        }

        /// <inheritdoc/>
        protected override WqlQueryMode QueryMode => WqlQueryMode.Translatable;

        /// <inheritdoc/>
        protected override IQueryable<RestApiSelectionItem> RetrieveItems(IQuery<Workspace> query, IQueryContext context, IRequest request)
        {
            var current = CalendarRequest.Workspace(request);

            return CoreHub.WorkspaceManager
                .GetWorkspaces(query, context)
                .Where(x => x.Id != current?.Id)
                .Where(x => AppointmentAuthorization.MayRead(x, request))
                .OrderBy(x => x.Name)
                .Select(x => new RestApiSelectionItem { Id = x.Id, Text = $"{x.Name} ({x.Key})" })
                .ToList()
                .AsQueryable();
        }

        /// <inheritdoc/>
        protected override IQuery<Workspace> Filter(string filter, IQuery<Workspace> query, IRequest request)
        {
            return string.IsNullOrWhiteSpace(filter) || filter == "null" ? query : query.WhereContainsIgnoreCase(x => x.Name, filter);
        }
    }
}
