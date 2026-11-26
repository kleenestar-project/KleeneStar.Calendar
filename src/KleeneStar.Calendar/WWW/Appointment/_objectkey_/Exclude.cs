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
    /// The page behind the dialog removing one occurrence from a series. It is opened as a modal; its form is a fragment scoped to it.
    /// </summary>
    [WebIcon<IconCalendarMinus>]
    [Title("kleenestar.calendar:exclude.title")]
    [Scope<IScopeGeneral>]
    public sealed class Exclude : IPage<VisualTreeWebApp>, IScope
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        public Exclude()
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

            AppointmentAuthorization.DemandWrite(CalendarRequest.Appointment(request), request);
        }
    }
}
