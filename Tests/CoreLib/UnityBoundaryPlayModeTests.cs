using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenUGD.Core;
using OpenUGD.Presenters;
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
