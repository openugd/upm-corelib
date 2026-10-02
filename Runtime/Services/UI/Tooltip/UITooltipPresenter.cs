using OpenUGD.Core.Presenters;

namespace OpenUGD.Services.UI.Tooltip
{
    /// <summary>
    /// Attaches a tooltip to a pointer-aware view: entering opens <typeparamref name="TTooltip"/> with this
    /// presenter's model, leaving closes it.
    /// </summary>
    /// <typeparam name="TTooltip">The presenter opened as the tooltip.</typeparam>
    /// <typeparam name="TTooltipModel">The model handed to it.</typeparam>
    public class UITooltipPresenter<TTooltip, TTooltipModel> : Presenter<IUITooltip, TTooltipModel>
        where TTooltip : Presenter, IPresenterWithModel<TTooltipModel>
    {
        private readonly UITooltipService _service;

        /// <summary>Creates the presenter.</summary>
        /// <param name="service">The service that opens the tooltip.</param>
        public UITooltipPresenter(UITooltipService service) => _service = service;

        /// <inheritdoc/>
        /// <remarks>
        /// Wiring, so it belongs here rather than in <see cref="Presenter{TView}.OnRefresh"/>: the
        /// subscription is made once per view, and <see cref="Presenter{TView,TModel}.Model"/> is read at
        /// pointer-enter time, so a later <c>SetModel</c> is picked up without re-subscribing.
        /// </remarks>
        protected override void OnViewAdded() =>
            View.SubscribeOnEnter(Lifetime, enter => {
                var reference = _service.Open<TTooltip, TTooltipModel>(Model);
                Lifetime.AddAction(reference.Close);
                View.SubscribeOnExit(Lifetime, exit => { reference.Close(); });
            });
    }
}
