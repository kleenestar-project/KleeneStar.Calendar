using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using System;
using WebExpress.WebCore;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebComponent;
using WebExpress.WebCore.WebPlugin;

namespace KleeneStar.Calendar
{
    /// <summary>
    /// Plugin entry point of the appointments: the object kind <c>appointment</c>, shown in the
    /// calendar of a workspace, with series, templates, participants and their answers, resource
    /// bookings, conflicts, calendars of other workspaces and of WQL queries, the import and
    /// export of iCalendar files and the closing of appointments whose time is over.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An appointment is an ordinary object of a class of the kind - its title, description,
    /// workflow, comments, attachments and history are the object's, and the core's detail
    /// experience shows them. What an object has no column for is kept in the model's
    /// appointment tables (<c>Appointment</c>, <c>AppointmentParticipant</c>,
    /// <c>AppointmentResource</c>, <c>AppointmentCalendarSource</c>), because the installation
    /// has one database and one migration history; without the plugin they are not read.
    /// </para>
    /// <para>
    /// The plugin's pages refuse a caller themselves (<see cref="AppointmentAuthorization"/>),
    /// since the core's route authorization judges the core's pages only.
    /// </para>
    /// </remarks>
    [Name("kleenestar.calendar:plugin.name")]
    [Description("kleenestar.calendar:plugin.description")]
    [Icon("/assets/img/kleenestar.calendar.svg")]
    [Application<KleeneStarApplication>()]
    [Dependency("webexpress.webapp")]
    [Dependency("kleenestar.core")]
    public sealed class KleeneStarPlugin : IPlugin
    {
        private readonly IHttpServerContext _httpServerContext;
        private readonly IPluginContext _pluginContext;
        private AppointmentCloser _closer;

        /// <summary>
        /// Initializes a new instance of the class. Invoked by WebExpress via reflection.
        /// </summary>
        /// <param name="httpServerContext">The server context, for the log.</param>
        /// <param name="pluginContext">The context of the plugin, for its settings.</param>
        /// <param name="componentHub">The component hub the plugin's manager is registered with.</param>
        public KleeneStarPlugin(IHttpServerContext httpServerContext, IPluginContext pluginContext, IComponentHub componentHub)
        {
            _httpServerContext = httpServerContext;
            _pluginContext = pluginContext;
            CalendarHub.ComponentHub = componentHub;
        }

        /// <summary>
        /// Connects the manager to the core's object events and starts closing the appointments
        /// whose time is over.
        /// </summary>
        public void Run()
        {
            try
            {
                CalendarHub.Configuration = _pluginContext?.Settings;

                var manager = CalendarHub.AppointmentManager
                    ?? throw new InvalidOperationException("The appointment manager is not registered.");

                manager.Connect();

                var interval = CalendarHub.Settings.AutoCloseInterval;

                if (interval > 0)
                {
                    _closer = new AppointmentCloser(TimeSpan.FromMinutes(interval));
                }
            }
            catch (Exception ex)
            {
                _httpServerContext?.Log?.Exception(ex);
            }
        }

        /// <summary>
        /// Stops closing appointments.
        /// </summary>
        public void Dispose()
        {
            _closer?.Dispose();
            _closer = null;
        }
    }
}
