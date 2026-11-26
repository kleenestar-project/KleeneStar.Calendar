using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebParameter;
using KleeneStar.Core;
using KleeneStar.Core.WebAttribute;
using System;
using WebExpress.WebApp.WebPage;
using WebExpress.WebApp.WebScope;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebPage;
using WebExpress.WebCore.WebScope;
using WebExpress.WebUI.WebIcon;

namespace KleeneStar.Calendar.WWW.Appointment._objectkey_
{
    /// <summary>
    /// The page behind the dialog in which an invited account answers. It is opened as a modal; its form is a fragment scoped to it.
    /// </summary>
    [WebIcon<IconReply>]
    [Title("kleenestar.calendar:respond.title")]
    [Scope<IScopeGeneral>]
    public sealed class Respond : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Respond()
        {
        }

        /// <summary>
        /// Refuses a caller who may not use the dialog.
        /// </summary>
        /// <param name="renderContext">The context for rendering the page.</param>
        /// <param name="visualTree">The visual tree of the web application.</param>
        public void Process(IRenderContext renderContext, VisualTreeWebApp visualTree)
        {
            var request = renderContext.Request;

            var appointment = CalendarRequest.Appointment(request);

            if (appointment is not null && !AppointmentAuthorization.MayRespond(appointment, request))
            {
                throw KleeneStar.Core.WebPermission.PageAuthorization.Refuse();
            }
        }
    }
}
