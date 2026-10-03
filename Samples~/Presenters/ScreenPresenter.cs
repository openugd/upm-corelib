using OpenUGD.Logging;
using OpenUGD.Presenters;

namespace OpenUGD.Samples.Presenters
{
    /// <summary>
    /// A presenter with no view: a node that groups two counters. Closing it closes them, children first.
    /// </summary>
    public sealed class ScreenPresenter : Presenter
    {
        [Inject] private ILog _log;

        public CounterPresenter First { get; private set; }

        public CounterPresenter Second { get; private set; }

        protected override void OnInitialize()
        {
            _log = _log.WithTag("Screen");

            // Children are attached under this presenter's Lifetime and injected by the same factory.
            First = AddPresenter(new CounterPresenter("First"));
            Second = AddPresenter(new CounterPresenter("Second"));

            // Registered after the children, so it runs before they close: a Lifetime unwinds newest first.
            Lifetime.AddAction(() => _log.Info("clean-up registered after the children: runs before they close"));

            _log.Info("initialized with two children");
        }

        // Last: after every child has closed and every clean-up registered on Lifetime has run.
        protected override void OnClose() => _log.Info("closed, after its children");
    }
}
