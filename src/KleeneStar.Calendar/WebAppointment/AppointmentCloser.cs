using System;
using System.Threading;

namespace KleeneStar.Calendar.WebAppointment
{
    /// <summary>
    /// Closes, at a fixed interval, the appointments whose last occurrence is over
    /// (<see cref="WebManager.IAppointmentManager.CloseExpired"/>).
    /// </summary>
    /// <remarks>
    /// The closing is idempotent - an appointment already in a closing state is only marked as
    /// looked at - so replicas running it at the same time move every appointment once: the
    /// second transition finds the state reached and is answered as unchanged.
    /// </remarks>
    public sealed class AppointmentCloser : IDisposable
    {
        private readonly Timer _timer;
        private int _running;

        /// <summary>
        /// Initializes a new instance of the class and starts the timer.
        /// </summary>
        /// <param name="interval">The interval.</param>
        public AppointmentCloser(TimeSpan interval)
        {
            var period = interval < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : interval;

            // the first round runs shortly after the start, so what ran out while the host was
            // down is closed without waiting a whole interval
            _timer = new Timer(_ => Tick(DateTime.UtcNow), null, TimeSpan.FromSeconds(30), period);
        }

        /// <summary>
        /// Runs one round, unless the previous one still runs.
        /// </summary>
        /// <param name="now">The present (UTC).</param>
        /// <returns>The number of appointments closed.</returns>
        public int Tick(DateTime now)
        {
            if (Interlocked.Exchange(ref _running, 1) == 1)
            {
                return 0;
            }

            try
            {
                return CalendarHub.AppointmentManager?.CloseExpired(now) ?? 0;
            }
            catch (Exception ex)
            {
                // the timer thread has nobody to report to but the log
                CalendarHub.ComponentHub?.LogManager?.DefaultLog?.Exception(ex);

                return 0;
            }
            finally
            {
                Volatile.Write(ref _running, 0);
            }
        }

        /// <summary>
        /// Stops the timer.
        /// </summary>
        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
