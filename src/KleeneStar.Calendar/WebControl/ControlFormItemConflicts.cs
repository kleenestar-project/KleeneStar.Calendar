using WebExpress.WebCore.Internationalization;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebControl
{
    /// <summary>
    /// The conflict panel of the appointment dialog: an empty box the client script
    /// (<c>kleenestar.calendar.conflicts.js</c>, controller <c>wx-kleenestar-calendar-conflicts</c>)
    /// fills with what the planned booking collides with, asking the conflict endpoint whenever
    /// the time, the series, the participants or the resources change.
    /// </summary>
    public sealed class ControlFormItemConflicts : ControlFormItem
    {
        /// <summary>
        /// Gets or sets the address of the conflict endpoint.
        /// </summary>
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the key of the appointment being changed, or <see langword="null"/>.
        /// </summary>
        public string Appointment { get; set; }

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="id">The id of the control.</param>
        public ControlFormItemConflicts(string id)
            : base(id)
        {
        }

        /// <summary>
        /// Initializes nothing: the panel has no value of its own.
        /// </summary>
        /// <param name="renderContext">The context in which the item is rendered.</param>
        public override void Initialize(IRenderControlFormContext renderContext)
        {
        }

        /// <summary>
        /// Renders the panel.
        /// </summary>
        /// <param name="renderContext">The context in which the item is rendered.</param>
        /// <param name="visualTree">The visual tree.</param>
        /// <returns>The panel.</returns>
        public override IHtmlNode Render(IRenderControlFormContext renderContext, IVisualTreeControl visualTree)
        {
            var element = new HtmlElementTextContentDiv()
                .AddClass("wx-kleenestar-calendar-conflicts", "mb-3")
                .AddUserAttribute("id", Id)
                .AddUserAttribute("data-uri", Uri ?? string.Empty)
                .AddUserAttribute("data-label", I18N.Translate(renderContext, "kleenestar.calendar:conflict.title"))
                .AddUserAttribute("data-none", I18N.Translate(renderContext, "kleenestar.calendar:conflict.none"))
                .AddUserAttribute("data-checking", I18N.Translate(renderContext, "kleenestar.calendar:conflict.checking"));

            if (!string.IsNullOrWhiteSpace(Appointment))
            {
                element.AddUserAttribute("data-appointment", Appointment);
            }

            return element;
        }
    }
}
