using OpenUGD.Logging;
using OpenUGD.Presenters;

namespace OpenUGD.Samples.Presenters
{
    /// <summary>
    /// Renders a number into an <see cref="ICounterView"/> and adds one on every click. Each hook logs, so the
    /// console shows the lifecycle.
    /// </summary>
    public sealed class CounterPresenter : Presenter<ICounterView, int>
    {
        private readonly string _name;

        // Filled by the tree's IPresenterFactory before OnInitialize - here ContextPresenterFactory, from the
        // context - although the presenter was created with new.
        [Inject] private ILog _log;

        public CounterPresenter(string name) => _name = name;

        protected override void OnInitialize()
        {
            _log = _log.WithTag(_name);
            _log.Info("attached and injected");
        }

        // Once per attached view: wire it, and register the unwiring on that view's own scope. ViewLifetime ends
        // just before the view is detached or replaced, so the old view is never left wired.
        protected override void OnViewAdded()
        {
            var view = View;
            view.Clicked += OnClicked;
            ViewLifetime.AddAction(() => {
                view.Clicked -= OnClicked;
                _log.Info($"unwired {view.Name}");
            });
            _log.Info($"wired {view.Name}");
        }

        // After every view attach and every model change, and only while live. Idempotent: it sets values.
        protected override void OnRefresh() => View.Show($"{_name}: {Model}");

        protected override void OnViewAfterRemoved() => _log.Info("view detached");

        protected override void OnClose() => _log.Info("closed");

        private void OnClicked()
        {
            SetModel(Model + 1);
            _log.Info($"clicked on {View.Name}, now {Model}");
        }
    }
}
