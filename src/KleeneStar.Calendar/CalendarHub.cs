using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Calendar.WebManager;
using KleeneStar.Core;
using Microsoft.Extensions.Configuration;
using WebExpress.WebCore.WebComponent;

namespace KleeneStar.Calendar
{
    /// <summary>
    /// The service locator of the calendar plugin, the counterpart of the core's
    /// <see cref="CoreHub"/>: the plugin's manager, resolved lazily from the component hub, and
    /// the plugin's settings.
    /// </summary>
    public static class CalendarHub
    {
        private static AppointmentManager _appointmentManager;
        private static AppointmentSettings _settings;

        /// <summary>
        /// Gets the component hub the plugin was started with; the core's when unset.
        /// </summary>
        public static IComponentHub ComponentHub { get; internal set; }

        /// <summary>
        /// Gets or sets the configuration section of the plugin.
        /// </summary>
        public static IConfiguration Configuration
        {
            get;
            internal set
            {
                field = value;
                _settings = null;
            }
        }

        /// <summary>
        /// Gets the settings of the plugin.
        /// </summary>
        public static AppointmentSettings Settings => _settings ??= AppointmentSettings.From(Configuration);

        /// <summary>
        /// Gets the manager of schedules, participants, bookings and calendar layers.
        /// </summary>
        public static IAppointmentManager AppointmentManager => _appointmentManager ??= (ComponentHub ?? CoreHub.ComponentHub)?.GetComponentManager<AppointmentManager>();
    }
}
