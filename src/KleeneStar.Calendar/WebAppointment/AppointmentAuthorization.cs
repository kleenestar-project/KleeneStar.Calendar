using KleeneStar.Core;
using KleeneStar.Core.WebPermission;
using KleeneStar.Core.WebPermissions;
using KleeneStar.Core.WebRestApi;
using KleeneStar.Model.Entities;
using System;
using WebExpress.WebCore.WebMessage;
using ObjectEntity = KleeneStar.Model.Entities.Object;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// The questions the plugin's pages and endpoints ask before they act - the same ones the
    /// core asks of its own kind pages, because the core's route authorization judges the core's
    /// pages only.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>The calendar of a workspace is its content: whoever may read the workspace's
    ///   content may open it (<see cref="WorkspaceReadContentPermission"/>).</item>
    ///   <item>An appointment is an object: it is read where the object manager answers it, and
    ///   changed by whoever may change the object (<see cref="ContentAuthorization.MayWrite"/>).
    ///   Creating one needs the right to create in its class.</item>
    ///   <item>An invited account answers for itself, whether or not it may change the
    ///   appointment.</item>
    ///   <item>The layers of a calendar are written by whoever may write the workspace's content
    ///   (<see cref="WorkspaceWriteContentPermission"/>); what a layer shows is read with the
    ///   reader's own visibility, so including a calendar reveals nothing.</item>
    /// </list>
    /// </remarks>
    public static class AppointmentAuthorization
    {
        /// <summary>
        /// Determines whether the caller may open the calendar of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the calendar may be shown.</returns>
        public static bool MayRead(Workspace workspace, IRequest request)
        {
            return workspace is not null
                && PageAuthorization.IsGranted(request, typeof(WorkspaceReadContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Refuses a caller who may not open the calendar of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        public static void DemandRead(Workspace workspace, IRequest request)
        {
            if (workspace is not null && !MayRead(workspace, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller may change the layers of the calendar of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the layers may be changed.</returns>
        public static bool MayArrange(Workspace workspace, IRequest request)
        {
            return workspace is not null
                && PageAuthorization.IsGranted(request, typeof(WorkspaceWriteContentPermission), PageAuthorization.ChainOf(workspace));
        }

        /// <summary>
        /// Refuses a caller who may not change the layers of the calendar of a workspace.
        /// </summary>
        /// <param name="workspace">The workspace.</param>
        /// <param name="request">The request.</param>
        public static void DemandArrange(Workspace workspace, IRequest request)
        {
            if (workspace is not null && !MayArrange(workspace, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller may plan appointments in a workspace: create one in
        /// some appointment class of it.
        /// </summary>
        /// <param name="class">The appointment class.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when an appointment may be created.</returns>
        public static bool MayCreate(Class @class, IRequest request)
        {
            return @class is not null && ContentAuthorization.MayCreateIn(@class.Id, request);
        }

        /// <summary>
        /// Determines whether the caller may change an appointment: its schedule, participants
        /// and bookings.
        /// </summary>
        /// <param name="appointment">The appointment object, read through the object manager.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the appointment may be changed.</returns>
        public static bool MayWrite(ObjectEntity appointment, IRequest request)
        {
            return appointment is not null && ContentAuthorization.MayWrite(appointment, request);
        }

        /// <summary>
        /// Refuses a caller who may not change an appointment.
        /// </summary>
        /// <param name="appointment">The appointment object.</param>
        /// <param name="request">The request.</param>
        public static void DemandWrite(ObjectEntity appointment, IRequest request)
        {
            if (appointment is not null && !MayWrite(appointment, request))
            {
                throw PageAuthorization.Refuse();
            }
        }

        /// <summary>
        /// Determines whether the caller is invited to an appointment and may therefore answer.
        /// </summary>
        /// <param name="appointment">The appointment object, read through the object manager.</param>
        /// <param name="request">The request.</param>
        /// <returns><see langword="true"/> when the caller is a participant.</returns>
        public static bool MayRespond(ObjectEntity appointment, IRequest request)
        {
            var identityId = CoreHub.SessionManager.GetCurrentIdentityId(request);

            if (appointment is null || identityId == Guid.Empty)
            {
                return false;
            }

            foreach (var participant in CalendarHub.AppointmentManager.GetParticipants(appointment.Id))
            {
                if (participant.IdentityId == identityId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
