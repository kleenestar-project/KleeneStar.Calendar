using KleeneStar.Calendar.WebAppointment;
using KleeneStar.Core;
using System.Linq;
using WebExpress.WebApp.WebSection;
using WebExpress.WebCore.WebAttribute;
using WebExpress.WebCore.WebFragment;
using WebExpress.WebCore.WebHtml;
using WebExpress.WebUI.WebControl;
using WebExpress.WebUI.WebFragment;
using WebExpress.WebUI.WebIcon;
using WebExpress.WebUI.WebPage;

namespace KleeneStar.Calendar.WebFragment.Calendar
{
    /// <summary>
    /// The headline button opening the dialog that plans a new appointment. It is shown to whoever
    /// may create an object in one of the workspace's appointment classes.
    /// </summary>
    [Section<SectionHeadlinePrimary>]
    [Scope<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Index>]
    [Order(0)]
    [Cache]
    public sealed class CalendarAddButtonFragment : FragmentControlButtonLink
    {
        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="fragmentContext">The context of the fragment.</param>
        public CalendarAddButtonFragment(IFragmentContext fragmentContext)
            : base(fragmentContext)
        {
            Text = _ => "kleenestar.calendar:appointment.add.label";
            Icon = _ => new IconPlus();
            Margin = _ => new PropertySpacingMargin(PropertySpacing.Space.Two);
            BackgroundColor = _ => new PropertyColorButton(TypeColorButton.Primary);
            PrimaryAction = renderContext => new ActionModal
            (
                "modal-form",
                CoreHub.GetUri<global::KleeneStar.Calendar.WWW.Calendar._workspacekey_.Add>()?
                    .BindParameters(renderContext.Request),
                TypeModalSize.ExtraLarge
            );
        }

        /// <summary>
        /// Renders the button, or nothing for a caller who may not plan here.
        /// </summary>
        /// <param name="renderContext">The context in which the fragment is rendered.</param>
        /// <param name="visualTree">The visual tree used for rendering the fragment.</param>
        /// <returns>The button, or <see langword="null"/>.</returns>
        public override IHtmlNode Render(IRenderControlContext renderContext, IVisualTreeControl visualTree)
        {
            var workspace = CalendarRequest.Workspace(renderContext?.Request);

            if (!CalendarRequest.Classes(workspace).Any(x => AppointmentAuthorization.MayCreate(x, renderContext?.Request)))
            {
                return null;
            }

            return base.Render(renderContext, visualTree);
        }
    }
}
