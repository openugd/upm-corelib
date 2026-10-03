using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using OpenUGD.Core;
using OpenUGD.Logging;
using OpenUGD.Presenters;
using UnityEngine;

namespace OpenUGD.Samples.Presenters
{
    /// <summary>
    /// The entry point: put it on an empty GameObject and press Play. It opens a presenter tree on a scope of its
    /// own, clicks, swaps a view, closes a presenter with its view, and closes the tree, one step a second.
    /// </summary>
    public sealed class PresentersSample : ContextBehaviour
    {
        private const float Pause = 1f;

        protected override bool PersistAcrossScenes => false;

        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            var log = new LogRoot("Presenters");
            log.UseUnityConsole(Lifetime);

            var builder = Context.CreateBuilder(Lifetime);
            builder.Services.AddInstance<ILog>(log);

            // Services and hosts take an IPresenterFactory, not the Context.
            builder.Services.Add<ContextPresenterFactory>().As<IPresenterFactory>();
            return builder.BuildAsync(cancellationToken);
        }

        protected override void OnStarted(Context context) => StartCoroutine(Run(context));

        private IEnumerator Run(Context context)
        {
            var log = context.Resolve<ILog>().WithTag("Host");
            var factory = context.Resolve<IPresenterFactory>();
            var viewA = CreateView("View A");
            var viewB = CreateView("View B");
            var viewC = CreateView("View C");

            log.Info("1. open: Attach the screen to a scope of the host's, then SetModel, then SetView");
            var screenScope = Lifetime.DefineNested("Screen");

            // Registered before the attach, so it runs after the screen's OnClose: where a host returns views to a
            // pool, once the presenters are done with them.
            screenScope.Lifetime.AddAction(() => log.Info("screen scope ended: the views can be released now"));

            var screen = new ScreenPresenter();
            Presenter.Attach(screen, screenScope, factory);
            screen.First.SetModel(0);
            screen.First.SetView(viewA);
            screen.Second.SetModel(10);
            screen.Second.SetView(viewB);
            yield return new WaitForSeconds(Pause);

            log.Info("2. click both views");
            viewA.Click();
            viewB.Click();
            yield return new WaitForSeconds(Pause);

            log.Info("3. swap First's view from View A to View C: View A is unwired by its ViewLifetime");
            screen.First.SetView(viewC);
            viewA.Click();
            viewC.Click();
            yield return new WaitForSeconds(Pause);

            log.Info("4. a popup that closes with its view: CloseWith(view.Lifetime), then destroy the view");
            var popupView = CreateView("Popup View");
            var popup = screen.AddPresenter(new CounterPresenter("Popup")).CloseWith(popupView.Lifetime);
            popup.SetView(popupView);
            Destroy(popupView.gameObject);
            yield return null; // Destroy takes effect at the end of the frame.
            log.Info($"the screen has {screen.Children.Count} children again");
            yield return new WaitForSeconds(Pause);

            log.Info("5. close the screen: the newest clean-up first, children before the parent's OnClose");
            screenScope.Terminate();
        }

        private CounterView CreateView(string viewName)
        {
            var view = new GameObject(viewName).AddComponent<CounterView>();
            view.transform.SetParent(transform, false);
            return view;
        }
    }
}
