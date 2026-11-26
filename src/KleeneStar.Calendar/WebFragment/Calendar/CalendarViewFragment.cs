using System.Net.Http;
using WebExpress.WebApp.WebControl;
using WebExpress.WebApp.WebData;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;

namespace KleeneStar.Calendar.WebFragment.Calendar
{
    /// <summary>
    /// The type-safe identity of the calendar resource shared inside the calendar's view state.
    /// </summary>
    public sealed class CalendarScheduleResource : IDataResource
    {
    }

    /// <summary>
    /// The calendar grid of a workspace: month, week (with a time axis) and agenda, fed by the
    /// calendar endpoint (<see cref="global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Index"/>)
    /// with the occurrences of the workspace's appointments and of its layers.
    /// </summary>
    /// <remarks>
    /// An entry can be dragged to another time - the endpoint moves a single appointment and
    /// refuses an occurrence of a series or an entry of a layer, which are changed in their own
    /// dialogs. Drawing a span on the grid plans a new appointment in it. Deleting is the object
    /// flow's and stays off.
    /// </remarks>
    [Section<SectionContentPrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class CalendarViewFragment : FragmentControlPanel
    {
        /// <summary>
        /// The message shown when the endpoint refuses a move (409); the calendar reloads the
        /// shown period, so the entry snaps back.
        /// </summary>
        public const string ConflictResource = "kleenestar.calendar:calendar.move.refused";

        /// <summary>
        /// Gets the calendar control of the view.
        /// </summary>
        public ControlDataSchedule Schedule { get; } = new ControlDataSchedule()
        {
            View = _ => TypeViewSchedule.Month,
            ShowWeekNumbers = _ => true,
            MiniCalendar = _ => true,
            TimeAxis = _ => true,
            HourStart = _ => 7,
            HourEnd = _ => 20,
            Editable = _ => true,
            Creatable = _ => true,
            Deletable = _ => false
        };

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarViewFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            var id = fragmentContext?.FragmentId?.ToString()?.Replace(".", "-");

            var viewState = new ControlViewState<DataQueryState>(id + "-viewstate")
                .State(_ => { })
                .Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Index>(service => service
                    .Method(HttpMethod.Get)
                    .Error(409, ConflictResource))
                .Resource<CalendarScheduleResource>(resource => resource
                    .Service<global::KleeneStar.Calendar.WWW.Api._1_.Calendar._workspacekey_.Index>());

            Schedule.Resource<CalendarScheduleResource>();

            Add(viewState, Schedule);
        }
    }
}
