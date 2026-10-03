using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenUGD.Core;
using OpenUGD.Presenters;
using OpenUGD.Utils;
using OpenUGD.Utils.Components;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace OpenUGD.Tests
{
    // The Unity half of the boundary: what only a real engine can show. Play Mode, because Unity calls Awake and
    // OnDestroy on an ordinary MonoBehaviour only in play mode. Level 1 skips these; level 2 runs them.
    [TestFixture]
    [Category("RequiresUnity")]
    public class UnityBoundaryPlayModeTests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly List<Lifetime.Definition> _scopes = new List<Lifetime.Definition>();

        [TearDown]
        public void TearDown()
        {
            for (var i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            for (var i = _scopes.Count - 1; i >= 0; i--)
            {
                _scopes[i].Terminate();
            }

            _created.Clear();
            _scopes.Clear();
        }

        // ------------------------------------------------------------------ ViewBehaviour (CC-5, UH-9)

        [Test]
        public void ViewBehaviour_Lifetime_EndsWhenTheGameObjectIsDestroyed()
        {
            var view = Create("view").AddComponent<ViewBehaviour>();
            var lifetime = view.Lifetime;

            Object.DestroyImmediate(view.gameObject);

            Assert.IsTrue(lifetime.IsTerminated);
        }

        [Test]
        public void ViewBehaviour_Lifetime_BeforeAwake_Throws()
        {
            var go = Create("inactive");
            go.SetActive(false);
            var view = go.AddComponent<ViewBehaviour>();

            Assert.Throws<InvalidOperationException>(() => { var _ = view.Lifetime; });
        }

        [Test]
        public void ViewBehaviour_APresenterClosedWithTheView_ClosesWhenTheViewIsDestroyed()
        {
            var view = Create("view").AddComponent<ViewBehaviour>();
            var root = new Presenter.Root(NewScope(), new PlainFactory());
            var presenter = root.AddPresenter(new GameObjectPresenter()).CloseWith(view.Lifetime);
            presenter.SetView(view.gameObject);

            Object.DestroyImmediate(view.gameObject);

            Assert.IsTrue(presenter.Lifetime.IsTerminated);
            Assert.IsFalse(root.Lifetime.IsTerminated);
        }

        [Test]
        public void ViewBehaviour_ASubclassThatOverridesAndCallsBase_KeepsItsScope()
        {
            var view = Create("view").AddComponent<OverridingView>();
            var lifetime = view.Lifetime;

            Object.DestroyImmediate(view.gameObject);

            Assert.IsTrue(view.AwakeRan);
            Assert.IsTrue(view.LifetimeWasLiveInOnDestroy, "base.OnDestroy() runs after the subclass's own code");
            Assert.IsTrue(lifetime.IsTerminated);
        }

        // ------------------------------------------------------------------ Presenter liveness (UH-12)

        [Test]
        public void Presenter_ADestroyedUnityView_IsNotRendered()
        {
            var go = Create("view");
            var presenter = new Presenter.Root(NewScope(), new PlainFactory()).AddPresenter(new GameObjectPresenter());
            presenter.SetView(go);
            Assert.AreEqual(1, presenter.RefreshCount);

            Object.DestroyImmediate(go);
            presenter.Refresh();

            Assert.AreEqual(1, presenter.RefreshCount, "Refresh must skip a view Unity has destroyed");
            Assert.IsNotNull((object)presenter.View, "the reference is still held; only Unity's liveness says no");
        }

        // ------------------------------------------------------------------ ContextBehaviour (CC-6, CC-7)

        [Test]
        public void ContextBehaviour_PersistsAcrossScenes_ByDefault()
        {
            var behaviour = Create("context").AddComponent<PendingContext>();

            Assert.AreEqual("DontDestroyOnLoad", behaviour.gameObject.scene.name);
        }

        [Test]
        public void ContextBehaviour_ThatDoesNotPersist_StaysInItsScene()
        {
            var behaviour = Create("context").AddComponent<SceneContext>();

            Assert.AreEqual(SceneManager.GetActiveScene(), behaviour.gameObject.scene);
        }

        [Test]
        public void ContextBehaviour_OnAChildObject_IsNotMovedAndSaysWhy()
        {
            var parent = Create("parent");
            var child = new GameObject("child");
            child.transform.SetParent(parent.transform);
            LogAssert.Expect(LogType.Warning, new Regex("not on a root object"));

            child.AddComponent<PendingContext>();

            Assert.AreEqual(SceneManager.GetActiveScene(), child.scene);
            Assert.AreSame(parent.transform, child.transform.parent);
        }

        [UnityTest]
        public IEnumerator ContextBehaviour_Destroyed_EndsItsScope_AndCancelsTheBoot()
        {
            var behaviour = Create("context").AddComponent<PendingContext>();
            var lifetime = behaviour.Lifetime;
            var startup = behaviour.Startup;

            Object.DestroyImmediate(behaviour.gameObject);
            Assert.IsTrue(lifetime.IsTerminated);

            // The boot's continuation is posted to Unity's synchronization context, so it ends on a later frame.
            for (var frame = 0; frame < 10 && !startup.IsCompleted; frame++) yield return null;

            Assert.IsTrue(startup.IsCanceled, "a boot in flight ends cancelled, not faulted");
        }

        [UnityTest]
        public IEnumerator ContextBehaviour_AnUpdateOverrideThatCallsBase_StillFiresOnUpdate()
        {
            var behaviour = Create("context").AddComponent<UpdatingContext>();
            var fired = 0;
            behaviour.OnUpdate.Subscribe(behaviour.Lifetime, () => fired++);

            yield return null;
            yield return null;

            Assert.Greater(behaviour.OwnUpdates, 0);
            Assert.Greater(fired, 0, "the override called base.Update(), so OnUpdate keeps firing");
        }

        // ------------------------------------------------------------------ play session (UH-11)

        [Test]
        public void ViewAndContext_NestTheirScopesInThePlaySession()
        {
            var view = Create("view").AddComponent<ViewBehaviour>();
            var context = Create("context").AddComponent<PendingContext>();

            EndThePlaySession();

            Assert.IsTrue(view.Lifetime.IsTerminated, "a view's scope ends with the play session");
            Assert.IsTrue(context.Lifetime.IsTerminated, "so does a context's, and the context with it");
        }

        [Test]
        public void SignalMonoBehaviour_ItsSignalsEndWithThePlaySession()
        {
            var component = Create("signals").AddComponent<SignalMonoBehaviour>();
            var enabled = 0;
            component.EnableSignal.Subscribe(Lifetime.Eternal, () => enabled++);

            EndThePlaySession();
            component.gameObject.SetActive(false);
            component.gameObject.SetActive(true);

            Assert.AreEqual(0, enabled, "the signals are scoped to the component's scope, which the session ended");
        }

        [Test]
        public void ContextBehaviour_WhenThePlaySessionEnds_FiresOnQuitOnce()
        {
            var behaviour = Create("context").AddComponent<QuittingContext>();
            var quits = 0;
            var scopeAliveDuringQuit = false;
            behaviour.OnQuit.Subscribe(Lifetime.Eternal, () => {
                quits++;
                scopeAliveDuringQuit = !behaviour.Lifetime.IsTerminated;
            });

            EndThePlaySession();
            behaviour.SendOnApplicationQuit();

            Assert.AreEqual(1, quits, "whichever of the two Unity does first fires OnQuit, and only once");
            Assert.IsTrue(scopeAliveDuringQuit, "OnQuit runs before the scope, and the context under it, end");
        }

        [Test]
        public void ContextBehaviour_OnApplicationQuitThenTheSessionEnd_FiresOnQuitOnce()
        {
            var behaviour = Create("context").AddComponent<QuittingContext>();
            var quits = 0;
            behaviour.OnQuit.Subscribe(Lifetime.Eternal, () => quits++);

            behaviour.SendOnApplicationQuit();
            EndThePlaySession();

            Assert.AreEqual(1, quits);
        }

        [Test]
        public void ContextBehaviour_DestroyedOrRebuilt_IsNotAQuit()
        {
            var behaviour = Create("context").AddComponent<QuittingContext>();
            var quits = 0;
            behaviour.OnQuit.Subscribe(Lifetime.Eternal, () => quits++);

            behaviour.Rebuild();
            Object.DestroyImmediate(behaviour.gameObject);

            Assert.AreEqual(0, quits);
        }

        // Stands in for Application.quitting, then starts the next session so later tests have a live one.
        private static void EndThePlaySession()
        {
            PlaySession.End();
            PlaySession.Begin();
        }

        // ------------------------------------------------------------------ boot failures (CC-28)

        [UnityTest]
        public IEnumerator ContextBehaviour_WhenOnStartedThrows_DisposesTheContext_AndLeavesContextNull()
        {
            var behaviour = Create("context").AddComponent<FailingStartContext>();

            for (var frame = 0; frame < 30 && !behaviour.Startup.IsCompleted; frame++) yield return null;

            Assert.IsTrue(behaviour.Startup.IsFaulted, "a throwing OnStarted faults Startup");
            Assert.IsInstanceOf<InvalidOperationException>(behaviour.Reported, "and reaches OnStartFailed");
            Assert.IsNotNull(behaviour.Built, "the context was built before OnStarted ran");
            Assert.IsNull(behaviour.Context, "a context whose OnStarted failed is not published");
            Assert.IsTrue(behaviour.Built.Lifetime.IsTerminated, "and is disposed, not left running");
            Assert.IsFalse(behaviour.Lifetime.IsTerminated, "the behaviour's own scope survives, so Rebuild can retry");
        }

        [Test]
        public void ContextBehaviour_Rebuild_WhenTheOldScopeFailsToTerminate_StillStartsTheNewBoot_ThenRethrows()
        {
            var behaviour = Create("context").AddComponent<CountingContext>();
            var old = behaviour.Lifetime;
            var oldStartup = behaviour.Startup;
            var failure = new InvalidOperationException("a service failed to dispose");
            old.AddAction(() => throw failure);

            var thrown = Assert.Throws<InvalidOperationException>(behaviour.Rebuild);

            Assert.AreSame(failure, thrown, "the teardown failure reaches the caller as itself");
            Assert.IsTrue(old.IsTerminated, "the old scope is terminated anyway");
            Assert.AreNotSame(old, behaviour.Lifetime);
            Assert.IsFalse(behaviour.Lifetime.IsTerminated, "a new scope is in place");
            Assert.AreNotSame(oldStartup, behaviour.Startup);
            Assert.AreEqual(2, behaviour.Boots, "and the new boot has started");
        }

        // ------------------------------------------------------------------ ICoroutineProvider (CC-8)

        [Test]
        public void ContextBehaviour_AsCoroutineProvider_OnAnInactiveObject_Throws_InsteadOfReturningNull()
        {
            var behaviour = Create("context").AddComponent<PendingContext>();
            behaviour.gameObject.SetActive(false);
            ICoroutineProvider provider = behaviour;

            // Unity's own StartCoroutine would log "Coroutine couldn't be started" and return null here.
            Assert.Throws<InvalidOperationException>(() => provider.StartCoroutine(Frames(1, () => { })));
        }

        [Test]
        public void ContextBehaviour_AsCoroutineProvider_WhenDestroyed_Throws()
        {
            var behaviour = Create("context").AddComponent<PendingContext>();
            ICoroutineProvider provider = behaviour;
            Object.DestroyImmediate(behaviour.gameObject);

            Assert.Throws<InvalidOperationException>(() => provider.StartCoroutine(Frames(1, () => { })));
        }

        [UnityTest]
        public IEnumerator ContextBehaviour_AsCoroutineProvider_RunsTheCoroutine_AndStopsIt()
        {
            ICoroutineProvider provider = Create("context").AddComponent<PendingContext>();
            var ran = 0;
            var stopped = 0;

            var running = provider.StartCoroutine(Frames(1, () => ran++));
            var stopping = provider.StartCoroutine(Frames(3, () => stopped++));
            Assert.IsNotNull(running);
            provider.StopCoroutine(stopping);
            for (var frame = 0; frame < 5; frame++) yield return null;

            Assert.AreEqual(1, ran);
            Assert.AreEqual(0, stopped, "a stopped coroutine does not reach its end");
        }

        [Test]
        public void CoroutineProvider_OnAnInactiveHost_Throws()
        {
            var host = Create("host").AddComponent<ViewBehaviour>();
            var provider = new CoroutineProvider(host);
            host.gameObject.SetActive(false);

            Assert.Throws<InvalidOperationException>(() => provider.StartCoroutine(Frames(1, () => { })));
        }

        private static IEnumerator Frames(int count, Action then)
        {
            for (var i = 0; i < count; i++) yield return null;
            then();
        }

        // ------------------------------------------------------------------ SignalMonoBehaviour (UH-9)

        [Test]
        public void SignalMonoBehaviour_AnOnDestroyOverrideThatCallsBase_StillRaisesDestroySignal()
        {
            var component = Create("signals").AddComponent<OverridingSignals>();
            var destroyed = 0;
            component.DestroySignal.Subscribe(Lifetime.Eternal, () => destroyed++);

            Object.DestroyImmediate(component.gameObject);

            Assert.AreEqual(1, destroyed);
            Assert.IsTrue(component.OwnDestroyRan);
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private Lifetime NewScope()
        {
            var definition = Lifetime.Eternal.DefineNested();
            _scopes.Add(definition);
            return definition.Lifetime;
        }
    }

    // MonoBehaviours a test adds with AddComponent are public top-level types, as in the Test Framework's own
    // examples.
    internal sealed class PlainFactory : IPresenterFactory
    {
        public Presenter Create(Type presenterType) => (Presenter)Activator.CreateInstance(presenterType);

        public void Inject(Presenter presenter)
        {
        }
    }

    internal sealed class GameObjectPresenter : Presenter<GameObject>
    {
        public int RefreshCount { get; private set; }

        protected override void OnRefresh() => RefreshCount++;
    }

    public sealed class OverridingView : ViewBehaviour
    {
        public bool AwakeRan { get; private set; }
        public bool LifetimeWasLiveInOnDestroy { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            AwakeRan = !Lifetime.IsTerminated;
        }

        protected override void OnDestroy()
        {
            LifetimeWasLiveInOnDestroy = !Lifetime.IsTerminated;
            base.OnDestroy();
        }
    }

    // A context whose boot never finishes on its own: destroying it is the only way it ends, cancelled.
    public class PendingContext : ContextBehaviour
    {
        /// <inheritdoc />
        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            var pending = new TaskCompletionSource<Context>();
            cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
            return pending.Task;
        }
    }

    // Lets a test send OnApplicationQuit, which only Unity can call.
    public sealed class QuittingContext : PendingContext
    {
        public void SendOnApplicationQuit() => OnApplicationQuit();
    }

    // Counts how many boots have started.
    public sealed class CountingContext : PendingContext
    {
        public int Boots { get; private set; }

        /// <inheritdoc />
        protected override Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            Boots++;
            return base.CreateContextAsync(cancellationToken);
        }
    }

    // Builds a real (empty) context, then fails in OnStarted; records instead of logging, so nothing is logged.
    public sealed class FailingStartContext : ContextBehaviour
    {
        public Context Built { get; private set; }
        public Exception Reported { get; private set; }

        protected override bool PersistAcrossScenes => false;

        /// <inheritdoc />
        protected override async Task<Context> CreateContextAsync(CancellationToken cancellationToken)
        {
            Built = await Context.CreateBuilder(Lifetime).BuildAsync(cancellationToken);
            return Built;
        }

        protected override void OnStarted(Context context) =>
            throw new InvalidOperationException("OnStarted failed");

        protected override void OnStartFailed(Exception exception) => Reported = exception;
    }

    public sealed class SceneContext : PendingContext
    {
        protected override bool PersistAcrossScenes => false;
    }

    public sealed class UpdatingContext : PendingContext
    {
        public int OwnUpdates { get; private set; }

        protected override void Update()
        {
            base.Update();
            OwnUpdates++;
        }
    }

    public sealed class OverridingSignals : SignalMonoBehaviour
    {
        public bool OwnDestroyRan { get; private set; }

        protected override void OnDestroy()
        {
            OwnDestroyRan = true;
            base.OnDestroy();
        }
    }
}
