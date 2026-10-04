using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace OpenUGD.Presenters.Tests
{
    // The presenter contract of 2.0: OnReady of 0.6.1 is gone, split into OnViewAdded and OnRefresh, and three
    // seams are added: a public Attach, IPresenterFactory and a view-scoped lifetime (ViewLifetime).
    //
    // The contract under test:
    //   OnViewAdded()  fires exactly once, when a view attaches. Never again for the same view.
    //                  A replacement view attaches, so it fires again for that view.
    //   OnRefresh()    idempotent. Fires after the view attaches, and on every model change.
    //                  Never fires while the view is absent, or while the view is not alive.
    //   ViewLifetime   one scope per attached view, ended before that view is detached or replaced.
    //   Liveness is a property of the presenter's own scope, not of the view type, so the whole suite
    //   runs headless with a fake view and no Unity runtime.
    //   No container: every tree is rooted with a hand-written IPresenterFactory (RecordingFactory), and the
    //   presenters assembly references nothing from the family but lifetime. Injection through
    //   OpenUGD.Context is ContextPresenterFactory's, tested with the Unity boundary in com.openugd.corelib.
    //   Deleted for good: OnReady, Presenter.Internal.Ready, ISubscribeNotify,
    //   IPresenterWithModel.ModelChanged, OnAfterModelChanged, Presenter.Context. The reflection fixture at the
    //   bottom fails loudly if any of them is reintroduced.
    [TestFixture]
    public class PresenterTests
    {
        private const BindingFlags DeclaredMembers = BindingFlags.Public
                                                     | BindingFlags.NonPublic
                                                     | BindingFlags.Instance
                                                     | BindingFlags.Static
                                                     | BindingFlags.DeclaredOnly;

        private List<Lifetime.Definition> _definitions;
        private RecordingFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _definitions = new List<Lifetime.Definition>();
            _factory = new RecordingFactory();
        }

        [TearDown]
        public void TearDown()
        {
            for (var i = _definitions.Count - 1; i >= 0; i--)
            {
                try
                {
                    _definitions[i].Terminate();
                }
                catch (Exception)
                {
                    // a presenter hook that threw during teardown must not mask the test result
                }
            }

            _definitions.Clear();
            _factory = null;
        }

        // ------------------------------------------------------------------ OnViewAdded

        [Test]
        public void OnViewAdded_DoesNotFire_BeforeAViewIsAttached()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());

            Assert.AreEqual(0, presenter.ViewAddedCount, "OnViewAdded fired without a view");
        }

        [Test]
        public void OnViewAdded_FiresExactlyOnce_WhenTheViewAttaches()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            var view = new FakeView();

            presenter.SetView(view);

            Assert.AreEqual(1, presenter.ViewAddedCount);
            Assert.AreSame(view, presenter.View);
        }

        [Test]
        public void OnViewAdded_DoesNotFireAgain_WhenTheSameViewIsSetRepeatedly()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            var view = new FakeView();

            presenter.SetView(view);
            presenter.SetView(view);
            presenter.SetView(view);

            Assert.AreEqual(1, presenter.ViewAddedCount, "OnViewAdded must fire once per view, not once per call");
        }

        [Test]
        public void OnViewAdded_FiresAgain_WhenTheViewIsReplaced()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            var first = new FakeView();
            var second = new FakeView();

            presenter.SetView(first);
            presenter.SetView(second);

            Assert.AreEqual(2, presenter.ViewAddedCount, "a replacement view is an attach and must be announced");
            Assert.AreSame(second, presenter.View);
        }

        [Test]
        public void OnViewAdded_PrecedesOnRefresh_WhenTheViewAttaches()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());

            presenter.SetView(new FakeView());

            CollectionAssert.AreEqual(
                new[] { "initialize", "view-added", "refresh" },
                presenter.Log,
                "wiring (OnViewAdded) must happen before the first render (OnRefresh)");
        }

        [Test]
        public void OnViewAdded_FiresThroughTheObjectTypedInterface_UsedByThePresenterServices()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            var view = new FakeView();

            ((IPresenterWithView)presenter).SetView(view);

            Assert.AreEqual(1, presenter.ViewAddedCount);
            Assert.AreSame(view, presenter.View);
        }

        [Test]
        public void ViewType_ReportsTheViewGenericArgument()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());

            Assert.AreEqual(typeof(FakeView), ((IPresenterWithView)presenter).ViewType);
        }

        // ------------------------------------------------------------------ OnRefresh

        [Test]
        public void OnRefresh_FiresOnce_WhenTheViewAttaches()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());

            presenter.SetView(new FakeView());

            Assert.AreEqual(1, presenter.RefreshCount);
        }

        [Test]
        public void OnRefresh_FiresOnEveryModelChange()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());
            presenter.SetView(new FakeView());

            Assert.AreEqual(1, presenter.RefreshCount, "attaching the view is the first refresh");

            presenter.SetModel(new Payload("a"));
            Assert.AreEqual(2, presenter.RefreshCount);

            presenter.SetModel(new Payload("b"));
            Assert.AreEqual(3, presenter.RefreshCount);

            presenter.SetModel(new Payload("c"));
            Assert.AreEqual(4, presenter.RefreshCount);
        }

        [Test]
        public void OnRefresh_FiresOnAModelChangeThroughTheObjectTypedInterface()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());
            presenter.SetView(new FakeView());

            ((IPresenterWithModel)presenter).SetModel(new Payload("a"));

            Assert.AreEqual(2, presenter.RefreshCount);
            Assert.AreEqual("a", presenter.View.Rendered);
        }

        [Test]
        public void OnRefresh_FiresExactlyOncePerTrigger_NeverTwiceForOneEvent()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());

            presenter.SetModel(new Payload("a"));
            presenter.SetView(new FakeView());

            Assert.AreEqual(1, presenter.RefreshCount,
                "a presenter that already has a model must refresh once on attach, not once per input");
        }

        [Test]
        public void OnRefresh_IsIdempotent_RepeatedRefreshesLeaveTheSameRenderedState()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());
            var view = new FakeView();
            var payload = new Payload("a");

            presenter.SetView(view);
            presenter.SetModel(payload);
            var afterFirstRender = view.Rendered;

            presenter.SetModel(payload);
            presenter.SetModel(payload);

            Assert.AreEqual(afterFirstRender, view.Rendered,
                "OnRefresh must render the current model, not accumulate across calls");
            Assert.AreEqual("a", view.Rendered);
            Assert.AreEqual(4, view.RenderCount, "one render per trigger: attach plus three model changes");
        }

        [Test]
        public void OnRefresh_IsNotCalled_WhileTheViewIsAbsent()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());

            presenter.SetModel(new Payload("a"));
            presenter.SetModel(new Payload("b"));

            Assert.AreEqual(0, presenter.RefreshCount, "OnRefresh must never run without a view");
        }

        [Test]
        public void SetModel_BeforeTheViewAttaches_DefersTheRefreshUntilTheViewIsThere()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());
            var view = new FakeView();

            presenter.SetModel(new Payload("a"));
            Assert.AreEqual(0, presenter.RefreshCount);

            presenter.SetView(view);

            Assert.AreEqual(1, presenter.RefreshCount, "the deferred refresh runs as soon as the view attaches");
            Assert.AreEqual("a", view.Rendered, "the model set before the attach must be the one rendered");
        }

        [Test]
        public void OnRefresh_IsNotCalled_OnAModelChangeAfterThePresentersScopeEnds()
        {
            // The 2.0.0 invariant: liveness is a property of the presenter's own scope, not of the view type.
            // Asking the view was tried and abandoned — it cannot work for views we do not own, such as
            // UnityEngine.UI.Button. Whoever creates a view ties the presenter's Lifetime to its destruction.
            var root = CreateRoot();
            var presenter = root.AddPresenter(new ModelPresenter());
            presenter.SetView(new FakeView());
            Assert.AreEqual(1, presenter.RefreshCount);

            presenter.Close();
            presenter.SetModel(new Payload("a"));
            presenter.SetModel(new Payload("b"));

            Assert.AreEqual(1, presenter.RefreshCount, "OnRefresh must not run once the presenter's scope has ended");
        }

        [Test]
        public void OnRefresh_IsNotCalled_WhenNoViewIsAttached()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());

            presenter.SetModel(new Payload("a"));

            Assert.AreEqual(0, presenter.RefreshCount, "a model change without a view must not produce a refresh");
        }

        [Test]
        public void OnRefresh_RunsAgain_WhenTheViewIsReplaced()
        {
            var presenter = CreateRoot().AddPresenter(new ModelPresenter());
            presenter.SetView(new FakeView());
            presenter.SetModel(new Payload("a"));
            Assert.AreEqual(2, presenter.RefreshCount);

            var replacement = new FakeView();
            presenter.SetView(replacement);

            Assert.AreEqual(2, presenter.ViewAddedCount);
            Assert.AreEqual(3, presenter.RefreshCount);
            Assert.AreEqual("a", replacement.Rendered, "the replacement view renders the current model");
        }

        // ------------------------------------------------------------------ guards

        [Test]
        public void SetView_ThroughTheInterface_WithTheWrongType_NamesThePresenterAndBothTypes()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());

            var thrown = Assert.Throws<ArgumentException>(() => ((IPresenterWithView)presenter).SetView("not a view"));

            Assert.AreEqual("view", thrown.ParamName);
            StringAssert.Contains("PresenterTests.ViewPresenter", thrown.Message);
            StringAssert.Contains("PresenterTests.FakeView", thrown.Message);
            StringAssert.Contains("System.String", thrown.Message);
            Assert.IsNull(presenter.View, "a rejected view must not be stored");
            Assert.AreEqual(0, presenter.ViewAddedCount);
        }

        [Test]
        public void SetView_ThroughTheInterface_WithNull_Detaches()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            presenter.SetView(new FakeView());

            ((IPresenterWithView)presenter).SetView(null);

            Assert.IsNull(presenter.View);
        }

        [Test]
        public void SetModel_ThroughTheInterface_WithTheWrongType_NamesThePresenterAndBothTypes()
        {
            var presenter = CreateRoot().AddPresenter(new ListModelPresenter());

            var thrown = Assert.Throws<ArgumentException>(() => ((IPresenterWithModel)presenter).SetModel(42));

            Assert.AreEqual("model", thrown.ParamName);
            StringAssert.Contains("PresenterTests.ListModelPresenter", thrown.Message);
            StringAssert.Contains("System.Collections.Generic.List<System.Int32>", thrown.Message,
                "types are spelled the C# way, not as List`1[[System.Int32, ...]]");
            StringAssert.Contains("System.Int32", thrown.Message);
        }

        [Test]
        public void SetModel_ThroughTheInterface_WithNullForAValueType_NamesTheProblem()
        {
            var presenter = CreateRoot().AddPresenter(new IntModelPresenter());
            presenter.SetModel(7);

            var thrown = Assert.Throws<ArgumentException>(() => ((IPresenterWithModel)presenter).SetModel(null));

            StringAssert.Contains("PresenterTests.IntModelPresenter", thrown.Message);
            StringAssert.Contains("null", thrown.Message);
            StringAssert.Contains("System.Int32", thrown.Message);
            Assert.AreEqual(7, presenter.Model, "a rejected model must not replace the current one");
        }

        [Test]
        public void SetModel_ThroughTheInterface_AcceptsNull_WhereTheModelTypeCanHoldIt()
        {
            var reference = CreateRoot().AddPresenter(new ModelPresenter());
            reference.SetModel(new Payload("a"));
            var nullable = CreateRoot().AddPresenter(new NullableModelPresenter());
            nullable.SetModel(3);

            ((IPresenterWithModel)reference).SetModel(null);
            ((IPresenterWithModel)nullable).SetModel(null);
            ((IPresenterWithModel)nullable).SetModel(5);

            Assert.IsNull(reference.Model);
            Assert.AreEqual(5, nullable.Model, "a boxed int is a model of an int? presenter");
        }

        [Test]
        public void SetView_BeforeAttach_Throws_AndLeavesNothingHalfApplied()
        {
            var presenter = new ViewPresenter();

            var thrown = Assert.Throws<InvalidOperationException>(() => presenter.SetView(new FakeView()));
            StringAssert.Contains("SetView was called before the presenter was attached", thrown.Message,
                "the message names the call that was early, not just the missing Lifetime");
            Assert.Throws<InvalidOperationException>(() => ((IPresenterWithView)presenter).SetView(new FakeView()));

            Assert.IsNull(presenter.View);
            CollectionAssert.IsEmpty(presenter.Log);
            CreateRoot().AddPresenter(presenter);
            presenter.SetView(new FakeView());
            CollectionAssert.AreEqual(new[] { "initialize", "view-added", "refresh" }, presenter.Log,
                "after a refused early SetView the presenter must behave as if nothing happened");
        }

        [Test]
        public void SetModel_BeforeAttach_IsKept_AndRenderedOnceAViewArrives()
        {
            var presenter = new ModelPresenter();
            presenter.SetModel(new Payload("early"));
            var view = new FakeView();

            CreateRoot().AddPresenter(presenter).SetView(view);

            Assert.AreEqual("early", view.Rendered);
            Assert.AreEqual(1, presenter.RefreshCount);
        }

        [Test]
        public void SetView_AfterClose_IsIgnored()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            var first = new FakeView();
            presenter.SetView(first);
            presenter.Close();
            presenter.Log.Clear();

            presenter.SetView(new FakeView());
            presenter.SetView(null);
            ((IPresenterWithView)presenter).SetView(new FakeView());

            CollectionAssert.IsEmpty(presenter.Log, "no hook runs on a closed presenter");
            Assert.AreSame(first, presenter.View, "a closed presenter keeps what it had and takes nothing new");
        }

        [Test]
        public void SetView_WhileThePresenterIsClosing_IsIgnored()
        {
            var presenter = CreateRoot().AddPresenter(new ViewPresenter());
            presenter.SetView(new FakeView());
            presenter.Lifetime.AddAction(() => presenter.SetView(new FakeView()));
            presenter.Log.Clear();

            presenter.Close();

            CollectionAssert.AreEqual(new[] { "close" }, presenter.Log);
        }

        [Test]
        public void IsLive_ADestroyedView_IsNotRendered_EvenWhileThePresentersScopeIsAlive()
        {
            // UnityEngine.Object makes a destroyed object Equals(null); FakeUnityView does the same, so this
            // pins the engine-free half of the check. The Unity half is in com.openugd.corelib.playmode.tests.
            var presenter = CreateRoot().AddPresenter(new UnityLikePresenter());
            var view = new FakeUnityView();
            presenter.SetView(view);
            presenter.SetModel(1);
            Assert.AreEqual(2, presenter.RefreshCount);

            view.Destroyed = true;
            presenter.SetModel(2);
            presenter.Refresh();

            Assert.AreEqual(2, presenter.RefreshCount, "OnRefresh must not write into a destroyed view");
            Assert.IsFalse(presenter.Lifetime.IsTerminated, "the check reads the view; it does not close anything");
        }

        // ------------------------------------------------------------------ ViewLifetime

        [Test]
        public void ViewLifetime_IsAliveInOnViewAdded_AndNestedInThePresentersLifetime()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            var view = new WiredView();

            presenter.SetView(view);

            Assert.IsNotNull(presenter.LastViewLifetime);
            Assert.IsFalse(presenter.LastViewLifetime.IsTerminated);
            Assert.AreEqual(1, view.Listeners);

            presenter.Close();

            Assert.IsTrue(presenter.LastViewLifetime.IsTerminated, "the view scope ends with the presenter");
            Assert.AreEqual(0, view.Listeners);
        }

        [Test]
        public void ViewLifetime_EndsBeforeTheViewIsReplaced_WhileViewStillHoldsTheOldView()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            var first = new WiredView();
            var second = new WiredView();
            presenter.SetView(first);
            var firstScope = presenter.LastViewLifetime;

            presenter.SetView(second);

            Assert.IsTrue(firstScope.IsTerminated);
            Assert.AreEqual(0, first.Listeners, "the replaced view must be unwired");
            Assert.AreEqual(1, second.Listeners);
            Assert.AreNotSame(firstScope, presenter.LastViewLifetime, "each view gets a scope of its own");
            CollectionAssert.AreEqual(
                new[] { "view-scope-ended, View is still the old view", "view-removed" },
                presenter.Log,
                "the view scope ends first, with View still set, and only then is the view detached");
        }

        [Test]
        public void ViewLifetime_EndsWhenTheViewIsDetached_AndThenThereIsNone()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            var view = new WiredView();
            presenter.SetView(view);
            var scope = presenter.LastViewLifetime;

            presenter.SetView(null);

            Assert.IsTrue(scope.IsTerminated);
            Assert.AreEqual(0, view.Listeners);
            Assert.Throws<InvalidOperationException>(() => presenter.ReadViewLifetime(),
                "with no view attached there is no view scope to register on");
        }

        [Test]
        public void ViewLifetime_ReattachingTheSameView_WiresItExactlyOnce()
        {
            // The defect it replaces: listeners registered on the presenter's Lifetime survived a detach, so a
            // re-attached view was subscribed twice.
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            var view = new WiredView();

            presenter.SetView(view);
            presenter.SetView(null);
            presenter.SetView(view);

            Assert.AreEqual(1, view.Listeners);
        }

        [Test]
        public void ViewLifetime_EndsBeforeOnClose_WhenThePresenterCloses()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            presenter.SetView(new WiredView());

            presenter.Close();

            CollectionAssert.AreEqual(new[] { "view-scope-ended, View is still the old view", "close" }, presenter.Log);
        }

        [Test]
        public void ViewLifetime_BeforeAnyView_Throws()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());

            Assert.Throws<InvalidOperationException>(() => presenter.ReadViewLifetime());
        }

        [Test]
        public void ViewLifetime_WhenItsCleanUpThrows_TheOldViewIsStillDetached_AndTheNewOneIsNotStored()
        {
            var presenter = CreateRoot().AddPresenter(new WiringPresenter());
            var failure = new InvalidOperationException("listener removal failed");
            presenter.WhenViewAdded = p => p.ReadViewLifetime().AddAction(() => throw failure);
            presenter.SetView(new WiredView());
            presenter.WhenViewAdded = null;

            var thrown = Assert.Throws<InvalidOperationException>(() => presenter.SetView(new WiredView()));

            Assert.AreSame(failure, thrown);
            Assert.IsNull(presenter.View, "the old view is detached even though its clean-up failed");
            CollectionAssert.DoesNotContain(presenter.Log, "view-removed",
                "the exception propagates before OnViewAfterRemoved");
        }

        // ------------------------------------------------------------------ CloseWith

        [Test]
        public void CloseWith_ClosesThePresenter_WhenTheLifetimeEnds()
        {
            var view = NewDefinition();
            var log = new List<string>();
            var presenter = CreateRoot().AddPresenter(new TreePresenter("p", log));

            var returned = presenter.CloseWith(view.Lifetime);
            view.Terminate();

            Assert.AreSame(presenter, returned);
            Assert.IsTrue(presenter.Lifetime.IsTerminated);
            CollectionAssert.AreEqual(new[] { "p:initialize", "p:close" }, log);
        }

        [Test]
        public void CloseWith_WhenThePresenterClosesFirst_TheLaterEndOfTheLifetimeChangesNothing()
        {
            var view = NewDefinition();
            var log = new List<string>();
            var presenter = CreateRoot().AddPresenter(new TreePresenter("p", log)).CloseWith(view.Lifetime);

            presenter.Close();
            Assert.DoesNotThrow(view.Terminate);

            CollectionAssert.AreEqual(new[] { "p:initialize", "p:close" }, log, "closed exactly once");
        }

        [Test]
        public void CloseWith_AnEndedLifetime_ClosesThePresenterAtOnce()
        {
            var view = NewDefinition();
            view.Terminate();
            var presenter = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));

            presenter.CloseWith(view.Lifetime);

            Assert.IsTrue(presenter.Lifetime.IsTerminated);
        }

        [Test]
        public void CloseWith_ClosingTheViewScope_ClosesTheSubtree_AndLeavesTheParent()
        {
            var view = NewDefinition();
            var parent = CreateRoot().AddPresenter(new TreePresenter("parent", new List<string>()));
            var presenter = parent.AddPresenter(new TreePresenter("p", new List<string>())).CloseWith(view.Lifetime);
            var child = presenter.AddPresenter(new TreePresenter("c", new List<string>()));

            view.Terminate();

            Assert.IsTrue(child.Lifetime.IsTerminated);
            Assert.IsFalse(parent.Lifetime.IsTerminated);
            CollectionAssert.IsEmpty(parent.Children, "the closed presenter unlinks itself from its parent");
        }

        [Test]
        public void CloseWith_RejectsNulls_AndAnUnattachedPresenter()
        {
            var attached = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));

            Assert.Throws<ArgumentNullException>(() => ((TreePresenter)null).CloseWith(NewDefinition().Lifetime));
            Assert.Throws<ArgumentNullException>(() => attached.CloseWith(null));
            Assert.Throws<InvalidOperationException>(
                () => new TreePresenter("x", new List<string>()).CloseWith(NewDefinition().Lifetime));
        }

        // ------------------------------------------------------------------ the tree

        [Test]
        public void AddPresenter_ReturnsTheSameInstanceAndInitialisesItImmediately()
        {
            var root = CreateRoot();
            var child = new TreePresenter("c", new List<string>());

            var returned = root.AddPresenter(child);

            Assert.AreSame(child, returned);
            CollectionAssert.AreEqual(new[] { "c:initialize" }, child.Log);
        }

        [Test]
        public void Tree_InitialisesTopDown()
        {
            var log = new List<string>();
            var root = CreateRoot();

            var parent = root.AddPresenter(new TreePresenter("p", log));
            var child = parent.AddPresenter(new TreePresenter("c", log));
            child.AddPresenter(new TreePresenter("g", log));

            CollectionAssert.AreEqual(new[] { "p:initialize", "c:initialize", "g:initialize" }, log);
        }

        [Test]
        public void Close_TerminatesChildrenBeforeThePresenterItself()
        {
            var log = new List<string>();
            var root = CreateRoot();
            var parent = root.AddPresenter(new TreePresenter("p", log));
            var child = parent.AddPresenter(new TreePresenter("c", log));
            child.AddPresenter(new TreePresenter("g", log));
            log.Clear();

            parent.Close();

            CollectionAssert.AreEqual(new[] { "g:close", "c:close", "p:close" }, log,
                "closing a presenter must terminate its children first, deepest first");
        }

        [Test]
        public void Close_WhatOnInitializeRegisteredAndAttached_UnwindsBeforeOnClose_NewestFirst()
        {
            // The documented teardown order holds for what a presenter sets up in OnInitialize too, which is
            // where it normally sets things up: Attach registers the teardown before Inject and OnInitialize
            // run, so everything OnInitialize registers or attaches unwinds first, newest first, then OnClose.
            var log = new List<string>();
            var presenter = CreateRoot().AddPresenter(new InitializingPresenter(log));
            log.Clear();

            presenter.Close();

            CollectionAssert.AreEqual(new[] { "child:close", "p:clean-up", "p:close" }, log);
        }

        [Test]
        public void Close_TerminatesTheLifetimesOfTheWholeSubtree()
        {
            var log = new List<string>();
            var root = CreateRoot();
            var parent = root.AddPresenter(new TreePresenter("p", log));
            var child = parent.AddPresenter(new TreePresenter("c", log));
            var grandChild = child.AddPresenter(new TreePresenter("g", log));

            parent.Close();

            Assert.IsTrue(parent.Lifetime.IsTerminated);
            Assert.IsTrue(child.Lifetime.IsTerminated);
            Assert.IsTrue(grandChild.Lifetime.IsTerminated);
        }

        [Test]
        public void Close_OnAChild_LeavesTheParentAliveAndDetachesTheChild()
        {
            var log = new List<string>();
            var root = CreateRoot();
            var parent = root.AddPresenter(new TreePresenter("p", log));
            var child = parent.AddPresenter(new TreePresenter("c", log));

            child.Close();

            Assert.IsTrue(child.Lifetime.IsTerminated);
            Assert.IsFalse(parent.Lifetime.IsTerminated, "closing a child must not close its parent");

            parent.Close();

            Assert.AreEqual(1, log.Count(entry => entry == "c:close"),
                "a closed child must be detached, so the parent cannot close it a second time");
        }

        [Test]
        public void Close_WhenOneCleanUpThrows_RethrowsThatException_AndStillClosesTheRest()
        {
            var log = new List<string>();
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", log));
            var first = parent.AddPresenter(new TreePresenter("a", log));
            var second = parent.AddPresenter(new TreePresenter("b", log));
            var failure = new InvalidOperationException("boom");
            first.Lifetime.AddAction(() => throw failure);

            var thrown = Assert.Throws<InvalidOperationException>(() => parent.Close());

            Assert.AreSame(failure, thrown, "a single failure must arrive as itself, not wrapped");
            Assert.IsTrue(parent.Lifetime.IsTerminated);
            Assert.IsTrue(first.Lifetime.IsTerminated);
            Assert.IsTrue(second.Lifetime.IsTerminated, "a failing sibling must not stop the others closing");
            CollectionAssert.IsSubsetOf(new[] { "a:close", "b:close", "p:close" }, log);
        }

        [Test]
        public void Close_WhenSeveralCleanUpsThrow_ReportsThemAsOneAggregate()
        {
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));
            var first = parent.AddPresenter(new TreePresenter("a", new List<string>()));
            var second = parent.AddPresenter(new TreePresenter("b", new List<string>()));
            first.Lifetime.AddAction(() => throw new InvalidOperationException("a"));
            second.Lifetime.AddAction(() => throw new InvalidOperationException("b"));

            var thrown = Assert.Throws<AggregateException>(() => parent.Close());

            Assert.AreEqual(2, thrown.Flatten().InnerExceptions.Count);
            Assert.IsTrue(first.Lifetime.IsTerminated);
            Assert.IsTrue(second.Lifetime.IsTerminated);
        }

        [Test]
        public void TerminatingTheOwningLifetime_ClosesTheWholeTree()
        {
            var log = new List<string>();
            var definition = NewDefinition();
            var root = CreateRoot(definition.Lifetime);
            var parent = root.AddPresenter(new TreePresenter("p", log));
            parent.AddPresenter(new TreePresenter("c", log));
            log.Clear();

            definition.Terminate();

            CollectionAssert.AreEqual(new[] { "c:close", "p:close" }, log);
        }

        // ------------------------------------------------------------------ Presenter.Root

        [Test]
        public void Root_IsAPresenterWithALiveLifetime()
        {
            var definition = NewDefinition();

            var root = CreateRoot(definition.Lifetime);

            Assert.AreEqual("Root", root.GetType().Name);
            Assert.IsFalse(root.Lifetime.IsTerminated);
        }

        [Test]
        public void Root_InitialisesAndTearsDownItsChildren()
        {
            var log = new List<string>();
            var definition = NewDefinition();
            var root = CreateRoot(definition.Lifetime);

            var child = root.AddPresenter(new TreePresenter("c", log));
            CollectionAssert.AreEqual(new[] { "c:initialize" }, log);

            definition.Terminate();

            CollectionAssert.AreEqual(new[] { "c:initialize", "c:close" }, log);
            Assert.IsTrue(child.Lifetime.IsTerminated);
        }

        [Test]
        public void Root_IsNotASpecialCase_ItsChildrenGetTheSameHooksAsChildrenDeeperInTheTree()
        {
            var root = CreateRoot();
            var middle = root.AddPresenter(new ViewPresenter());

            var underRoot = root.AddPresenter(new ViewPresenter());
            var underMiddle = middle.AddPresenter(new ViewPresenter());

            underRoot.SetView(new FakeView());
            underMiddle.SetView(new FakeView());
            underRoot.Close();
            underMiddle.Close();

            CollectionAssert.AreEqual(
                new[] { "initialize", "view-added", "refresh", "close" },
                underRoot.Log,
                "a presenter directly under Root must receive every lifecycle hook");
            CollectionAssert.AreEqual(underMiddle.Log, underRoot.Log,
                "Root must not silently skip a hook that a normal parent delivers");
        }

        [Test]
        public void Root_DeclaresNoLifecycleHookOfItsOwn()
        {
            var rootType = typeof(Presenter).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(type => type.Name == "Root");
            Assert.IsNotNull(rootType, "Presenter.Root must still exist");

            var hooks = rootType.GetMethods(DeclaredMembers)
                .Select(method => method.Name)
                .Where(name => name == "OnInitialize"
                               || name == "OnViewAdded"
                               || name == "OnRefresh"
                               || name == "OnClose")
                .ToList();

            CollectionAssert.IsEmpty(hooks,
                "Root overriding a lifecycle hook is exactly how it became a special case before");
        }

        // ------------------------------------------------------------------ the deleted surface

        [Test]
        public void ISubscribeNotify_NoLongerExists()
        {
            var named = PresenterAssemblyTypes()
                .Where(type => type.Name == "ISubscribeNotify")
                .Select(type => type.FullName)
                .ToList();

            CollectionAssert.IsEmpty(named, "ISubscribeNotify was deleted with the OnReady latch");

            var implemented = PresenterTypes()
                .SelectMany(type => type.GetInterfaces())
                .Where(contract => contract.Name == "ISubscribeNotify")
                .Select(contract => contract.FullName)
                .Distinct()
                .ToList();

            CollectionAssert.IsEmpty(implemented, "no presenter may implement a notify contract any more");
        }

        [Test]
        public void OnReady_NoLongerExists()
        {
            var found = DeclaredMembersNamed("OnReady", PresenterTypes());

            CollectionAssert.IsEmpty(found,
                "OnReady was replaced by OnViewAdded plus OnRefresh and must not come back");
        }

        [Test]
        public void PresenterInternalReady_NoLongerExists()
        {
            var candidates = new List<Type> { typeof(Presenter) };
            candidates.AddRange(typeof(Presenter).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic));

            var found = DeclaredMembersNamed("Ready", candidates);

            CollectionAssert.IsEmpty(found, "the Presenter.Internal.Ready push path was deleted");
        }

        [Test]
        public void IPresenterWithModel_ModelChanged_NoLongerExists()
        {
            var contracts = new List<Type>
            {
                typeof(IPresenterWithModel),
                typeof(IPresenterWithModel<>),
                typeof(IPresenterWithView)
            };

            CollectionAssert.IsEmpty(DeclaredMembersNamed("ModelChanged", contracts),
                "ModelChanged was the latch's flag and went with it");
            CollectionAssert.IsEmpty(DeclaredMembersNamed("ModelChanged", PresenterTypes()),
                "no presenter may expose ModelChanged any more");
        }

        [Test]
        public void OnAfterModelChanged_NoLongerExists()
        {
            var contracts = new List<Type> { typeof(Presenter), typeof(Presenter<>), typeof(Presenter<,>) };

            CollectionAssert.IsEmpty(DeclaredMembersNamed("OnAfterModelChanged", contracts),
                "OnAfterModelChanged is a clean break, not a shim: a shim called from an overridable " +
                "OnRefresh stops rendering the moment a subclass forgets base.OnRefresh()");
            CollectionAssert.IsEmpty(DeclaredMembersNamed("OnAfterModelChanged", PresenterTypes()));
        }

        [Test]
        public void OnViewAddedAndOnRefresh_AreTheTwoOverridableHooks()
        {
            AssertHook("OnViewAdded");
            AssertHook("OnRefresh");
        }

        // ------------------------------------------------------------------ attachment

        [Test]
        public void Attach_InjectsThroughTheFactory_OnceAndBeforeOnInitialize()
        {
            var root = CreateRoot();
            var presenter = new InitializeProbe(_factory);

            root.AddPresenter(presenter);

            Assert.AreEqual(1, _factory.Injected.Count(p => p == presenter), "one Inject per attached presenter");
            Assert.IsTrue(presenter.WasInjectedBeforeInitialize,
                "the factory must inject before OnInitialize, which may already use what was injected");
        }

        [Test]
        public void Attach_HasTheLifetimeInPlace_WhenTheFactoryInjects()
        {
            Lifetime seen = null;
            _factory.OnInject = presenter => seen = presenter.Lifetime;

            var child = CreateRoot().AddPresenter(new TreePresenter("c", new List<string>()));

            Assert.AreSame(child.Lifetime, seen);
        }

        [Test]
        public void AddPresenter_InjectsEveryDescendant_WithTheFactoryTheTreeWasRootedWith()
        {
            var root = CreateRoot();
            var child = root.AddPresenter(new TreePresenter("c", new List<string>()));
            var grandChild = child.AddPresenter(new TreePresenter("g", new List<string>()));

            CollectionAssert.AreEqual(new Presenter[] { root, child, grandChild }, _factory.Injected);
        }

        [Test]
        public void TheTree_NeverCallsCreate()
        {
            var root = CreateRoot();
            root.AddPresenter(new ViewPresenter()).SetView(new FakeView());
            root.Close();

            CollectionAssert.IsEmpty(_factory.Created, "Create is for hosts that know a presenter only by its type");
        }

        [Test]
        public void Attach_IsPublic_AndRootsAPresenterOnTheCallersScope()
        {
            var definition = NewDefinition();
            var presenter = new ParentProbe();

            Presenter.Attach(presenter, definition, _factory);

            Assert.AreSame(definition.Lifetime, presenter.Lifetime);
            Assert.IsNull(presenter.ParentSeen, "a presenter attached with Attach belongs to no parent");
            CollectionAssert.AreEqual(new Presenter[] { presenter }, _factory.Injected);
        }

        [Test]
        public void Attach_TheDefinitionAndThePresenterEndTogether()
        {
            var first = NewDefinition();
            var closedByDefinition = new TreePresenter("a", new List<string>());
            Presenter.Attach(closedByDefinition, first, _factory);
            var second = NewDefinition();
            var closedByClose = new TreePresenter("b", new List<string>());
            Presenter.Attach(closedByClose, second, _factory);

            first.Terminate();
            closedByClose.Close();

            CollectionAssert.AreEqual(new[] { "a:initialize", "a:close" }, closedByDefinition.Log);
            Assert.IsTrue(second.IsTerminated, "Close terminates the definition the presenter was given");
        }

        [Test]
        public void Attach_CleanUpTheHostRegisteredBeforeTheAttach_RunsAfterOnClose()
        {
            // What a pooled view source relies on: it registers the view's release on the open's scope before
            // the presenter attaches, so the release runs once the presenter is done with the view.
            var definition = NewDefinition();
            var log = new List<string>();
            definition.Lifetime.AddAction(() => log.Add("host:release-view"));
            var presenter = new TreePresenter("p", log);
            Presenter.Attach(presenter, definition, _factory);
            presenter.Lifetime.AddAction(() => log.Add("p:own-clean-up"));
            log.Clear();

            definition.Terminate();

            CollectionAssert.AreEqual(new[] { "p:own-clean-up", "p:close", "host:release-view" }, log);
        }

        [Test]
        public void Attach_ChildrenOfAnAttachedPresenter_AreInjectedByItsFactory()
        {
            var other = new RecordingFactory();
            var presenter = new TreePresenter("p", new List<string>());
            Presenter.Attach(presenter, NewDefinition(), other);

            var child = presenter.AddPresenter(new TreePresenter("c", new List<string>()));

            CollectionAssert.AreEqual(new Presenter[] { presenter, child }, other.Injected);
            CollectionAssert.IsEmpty(_factory.Injected);
        }

        [Test]
        public void Attach_RejectsNullArguments()
        {
            var presenter = new TreePresenter("p", new List<string>());

            Assert.Throws<ArgumentNullException>(() => Presenter.Attach(null, NewDefinition(), _factory));
            Assert.Throws<ArgumentNullException>(() => Presenter.Attach(presenter, null, _factory));
            Assert.Throws<ArgumentNullException>(() => Presenter.Attach(presenter, NewDefinition(), null));
            Assert.Throws<ArgumentNullException>(() => new Presenter.Root(NewDefinition().Lifetime, null));
            Assert.Throws<ArgumentNullException>(() => new Presenter.Root(null, _factory));
            CollectionAssert.IsEmpty(_factory.Injected, "a rejected attach must not reach the factory");
        }

        [Test]
        public void Attach_ToATerminatedDefinition_Throws_AndInjectsNothing()
        {
            var definition = NewDefinition();
            definition.Terminate();
            var presenter = new TreePresenter("p", new List<string>());

            Assert.Throws<InvalidOperationException>(() => Presenter.Attach(presenter, definition, _factory),
                "a host must check IsTerminated before attaching, and skip the whole open, for this reason");
            CollectionAssert.IsEmpty(presenter.Log, "OnInitialize must not run");
            CollectionAssert.IsEmpty(_factory.Injected);
        }

        [Test]
        public void Attach_APresenterAttachedThroughAddPresenter_CannotBeAttachedAgain()
        {
            var child = CreateRoot().AddPresenter(new TreePresenter("c", new List<string>()));

            Assert.Throws<InvalidOperationException>(() => Presenter.Attach(child, NewDefinition(), _factory));
        }

        [Test]
        public void Attach_AnExceptionFromTheFactory_ReachesTheCaller()
        {
            var failure = new InvalidOperationException("cannot inject");
            _factory.OnInject = _ => throw failure;
            var presenter = new TreePresenter("p", new List<string>());

            var thrown = Assert.Throws<InvalidOperationException>(
                () => Presenter.Attach(presenter, NewDefinition(), _factory));

            Assert.AreSame(failure, thrown);
            CollectionAssert.IsEmpty(presenter.Log, "OnInitialize must not run when injection failed");
        }

        // ------------------------------------------------------------------ a failed attach is undone

        [Test]
        public void AddPresenter_WhenOnInitializeThrows_LeavesNoZombieChild()
        {
            var log = new List<string>();
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", log));
            var failure = new InvalidOperationException("cannot initialize");
            var child = new FailingInitializePresenter(log, failure);

            var thrown = Assert.Throws<InvalidOperationException>(() => parent.AddPresenter(child));

            Assert.AreSame(failure, thrown, "the failure reaches the caller as itself");
            CollectionAssert.IsEmpty(parent.Children, "a child whose OnInitialize threw must not stay attached");
            Assert.IsTrue(child.Lifetime.IsTerminated, "its scope is ended, not left alive under the parent");
            Assert.IsFalse(parent.Lifetime.IsTerminated, "the parent is unaffected");
            CollectionAssert.AreEqual(new[] { "p:initialize", "f:initialize", "f:clean-up" }, log,
                "what OnInitialize registered before it threw runs; OnClose does not, OnInitialize never completed");
        }

        [Test]
        public void AddPresenter_WhenTheFactoryThrows_LeavesNoZombieChild()
        {
            var log = new List<string>();
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", log));
            var child = new TreePresenter("c", log);
            _factory.OnInject = presenter => {
                if (presenter == child) throw new InvalidOperationException("cannot inject");
            };

            Assert.Throws<InvalidOperationException>(() => parent.AddPresenter(child));

            CollectionAssert.IsEmpty(parent.Children);
            Assert.IsTrue(child.Lifetime.IsTerminated);
            CollectionAssert.AreEqual(new[] { "p:initialize" }, log, "neither OnInitialize nor OnClose ran");

            log.Clear();
            parent.Close();
            CollectionAssert.AreEqual(new[] { "p:close" }, log, "closing the parent later does not reach the child");
        }

        [Test]
        public void AddPresenter_AfterAFailedAttach_TheParentStillTakesChildren()
        {
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));
            Assert.Throws<InvalidOperationException>(() => parent.AddPresenter(
                new FailingInitializePresenter(new List<string>(), new InvalidOperationException("boom"))));

            var sibling = parent.AddPresenter(new TreePresenter("s", new List<string>()));

            CollectionAssert.AreEqual(new Presenter[] { sibling }, parent.Children);
        }

        [Test]
        public void Attach_WhenOnInitializeThrows_TerminatesTheDefinition_AndRunsTheHostsCleanUp()
        {
            // A host that registered the release of a pooled view before the attach gets the view back.
            var definition = NewDefinition();
            var log = new List<string>();
            definition.Lifetime.AddAction(() => log.Add("host:release-view"));
            var presenter = new FailingInitializePresenter(log, new InvalidOperationException("boom"));

            Assert.Throws<InvalidOperationException>(() => Presenter.Attach(presenter, definition, _factory));

            Assert.IsTrue(definition.IsTerminated, "the presenter took the definition over, and it failed");
            CollectionAssert.AreEqual(new[] { "f:initialize", "f:clean-up", "host:release-view" }, log);
            Assert.Throws<InvalidOperationException>(() => Presenter.Attach(presenter, NewDefinition(), _factory),
                "a presenter whose attach failed stays closed");
        }

        [Test]
        public void Attach_WhenUndoingAFailedAttachThrowsToo_ReportsBoth_AttachFailureFirst()
        {
            var failure = new InvalidOperationException("cannot initialize");
            var undo = new InvalidOperationException("cannot clean up");
            var definition = NewDefinition();
            var presenter = new FailingInitializePresenter(new List<string>(), failure, undo);

            var thrown = Assert.Throws<AggregateException>(() => Presenter.Attach(presenter, definition, _factory));

            CollectionAssert.AreEqual(new Exception[] { failure, undo }, thrown.InnerExceptions);
            Assert.IsTrue(definition.IsTerminated);
        }

        [Test]
        public void Close_WhenOnCloseThrows_StillUnlinksFromTheParent()
        {
            var log = new List<string>();
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", log));
            var failure = new InvalidOperationException("cannot close");
            var child = parent.AddPresenter(new FailingClosePresenter(failure));

            var thrown = Assert.Throws<InvalidOperationException>(() => child.Close());

            Assert.AreSame(failure, thrown);
            Assert.IsTrue(child.Lifetime.IsTerminated);
            CollectionAssert.IsEmpty(parent.Children, "a presenter whose OnClose threw is closed and unlinked");
            log.Clear();
            Assert.DoesNotThrow(() => parent.Close(), "the parent must not close the child a second time");
            CollectionAssert.AreEqual(new[] { "p:close" }, log);
        }

        [Test]
        public void ReadingLifetimeBeforeAttach_ThrowsInsteadOfReturningNull()
        {
            var presenter = new TreePresenter("x", new List<string>());

            Assert.Throws<InvalidOperationException>(() => { var _ = presenter.Lifetime; });
            Assert.Throws<InvalidOperationException>(presenter.Close);
        }

        [Test]
        public void Presenter_ExposesNoContainer()
        {
            var members = new[] { typeof(Presenter), typeof(Presenter<>), typeof(Presenter<,>) }
                .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.NonPublic |
                                                    BindingFlags.Instance | BindingFlags.Static))
                .Where(member => member.Name == "Context" || member.Name == "Factory")
                .Select(member => member.DeclaringType + "." + member.Name)
                .ToList();

            CollectionAssert.IsEmpty(members,
                "a presenter reaches a service through an injected member; the container stays outside the tree");
        }

        [Test]
        public void AddPresenter_AfterClose_ThrowsInsteadOfAttachingToADeadScope()
        {
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));
            parent.Close();

            Assert.Throws<InvalidOperationException>(
                () => parent.AddPresenter(new TreePresenter("c", new List<string>())));
        }

        [Test]
        public void AddPresenter_AfterClose_LeavesNoHalfAttachedChild()
        {
            // Lifetime 2.0.0 no longer throws from DefineNested on a terminated lifetime; it returns a scope
            // that is born terminated. The guard has to come before the child is linked.
            var parent = CreateRoot().AddPresenter(new TreePresenter("p", new List<string>()));
            parent.Close();
            var orphan = new TreePresenter("c", new List<string>());

            Assert.Throws<InvalidOperationException>(() => parent.AddPresenter(orphan));

            CollectionAssert.IsEmpty(parent.Children, "a refused child must not be linked into the closed parent");
            Assert.DoesNotThrow(() => CreateRoot().AddPresenter(orphan),
                "a refused child was never attached, so it can still be attached to a live presenter");
        }

        [Test]
        public void Root_OnATerminatedLifetime_Throws()
        {
            // Root defines its scope with DefineNested, which Lifetime 2.0.0 no longer makes throw on a dead
            // lifetime. The InvalidOperationException Root documents must still come.
            var definition = NewDefinition();
            definition.Terminate();

            Assert.Throws<InvalidOperationException>(() => CreateRoot(definition.Lifetime));
        }

        [Test]
        public void AttachingTheSamePresenterTwice_Throws()
        {
            var root = CreateRoot();
            var presenter = root.AddPresenter(new TreePresenter("c", new List<string>()));

            Assert.Throws<InvalidOperationException>(() => root.AddPresenter(presenter));
        }

        // ------------------------------------------------------------------ helpers

        private Lifetime.Definition NewDefinition()
        {
            var definition = Lifetime.Eternal.DefineNested();
            _definitions.Add(definition);
            return definition;
        }

        private Presenter CreateRoot() => CreateRoot(NewDefinition().Lifetime);

        // The single construction site for a presenter tree.
        private Presenter CreateRoot(Lifetime lifetime) => new Presenter.Root(lifetime, _factory);

        private static void AssertHook(string name)
        {
            var hook = new[] { typeof(Presenter), typeof(Presenter<>), typeof(Presenter<,>) }
                .SelectMany(type => type.GetMethods(DeclaredMembers))
                .FirstOrDefault(method => method.Name == name);

            Assert.IsNotNull(hook, $"{name} must be declared on the Presenter hierarchy");
            Assert.IsTrue(hook.IsVirtual && !hook.IsAbstract, $"{name} must be virtual and have an empty default");
            Assert.IsTrue(hook.IsFamily || hook.IsFamilyOrAssembly, $"{name} must be protected");
            Assert.AreEqual(typeof(void), hook.ReturnType, $"{name} must return void");
            CollectionAssert.IsEmpty(hook.GetParameters(), $"{name} must take no arguments");
        }

        private static List<string> DeclaredMembersNamed(string name, IEnumerable<Type> types) =>
            types.SelectMany(type => type.GetMembers(DeclaredMembers)
                    .Where(member => member.Name == name)
                    .Select(member => $"{type.FullName}.{member.Name}"))
                .Distinct()
                .ToList();

        private static IEnumerable<Type> PresenterTypes() =>
            PresenterAssemblyTypes().Where(type => typeof(Presenter).IsAssignableFrom(type));

        private static IEnumerable<Type> PresenterAssemblyTypes()
        {
            try
            {
                return typeof(Presenter).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        // ------------------------------------------------------------------ fakes

        private sealed class FakeView
        {
            public string Rendered { get; private set; }
            public int RenderCount { get; private set; }

            public void Render(string text)
            {
                Rendered = text;
                RenderCount++;
            }
        }

        private sealed class Payload
        {
            public Payload(string text) => Text = text;

            public string Text { get; }
        }

        private class ViewPresenter : Presenter<FakeView>
        {
            public readonly List<string> Log = new List<string>();

            public int ViewAddedCount { get; private set; }
            public int RefreshCount { get; private set; }

            protected override void OnInitialize() => Log.Add("initialize");

            protected override void OnViewAdded()
            {
                ViewAddedCount++;
                Log.Add("view-added");
            }

            protected override void OnRefresh()
            {
                Assert.IsNotNull(View, "OnRefresh ran with no view attached");
                RefreshCount++;
                Log.Add("refresh");
            }

            protected override void OnClose() => Log.Add("close");
        }

        private sealed class ModelPresenter : Presenter<FakeView, Payload>
        {
            public int ViewAddedCount { get; private set; }
            public int RefreshCount { get; private set; }

            protected override void OnViewAdded() => ViewAddedCount++;

            protected override void OnRefresh()
            {
                Assert.IsNotNull(View, "OnRefresh ran with no view attached");
                RefreshCount++;
                View.Render(Model?.Text);
            }
        }

        // Sets up in OnInitialize, as a real presenter does: a clean-up on its Lifetime, then a child.
        private sealed class InitializingPresenter : Presenter
        {
            private readonly List<string> _log;

            public InitializingPresenter(List<string> log) => _log = log;

            protected override void OnInitialize()
            {
                Lifetime.AddAction(() => _log.Add("p:clean-up"));
                AddPresenter(new TreePresenter("child", _log));
            }

            protected override void OnClose() => _log.Add("p:close");
        }

        private sealed class TreePresenter : Presenter
        {
            private readonly string _name;

            public TreePresenter(string name, List<string> log)
            {
                _name = name;
                Log = log;
            }

            public List<string> Log { get; }

            protected override void OnInitialize() => Log.Add($"{_name}:initialize");

            protected override void OnClose() => Log.Add($"{_name}:close");
        }

        // Registers a clean-up, then throws from OnInitialize — optionally from the clean-up too.
        private sealed class FailingInitializePresenter : Presenter
        {
            private readonly List<string> _log;
            private readonly Exception _failure;
            private readonly Exception _cleanUpFailure;

            public FailingInitializePresenter(List<string> log, Exception failure, Exception cleanUpFailure = null)
            {
                _log = log;
                _failure = failure;
                _cleanUpFailure = cleanUpFailure;
            }

            protected override void OnInitialize()
            {
                _log.Add("f:initialize");
                Lifetime.AddAction(() => {
                    _log.Add("f:clean-up");
                    if (_cleanUpFailure != null) throw _cleanUpFailure;
                });
                throw _failure;
            }

            protected override void OnClose() => _log.Add("f:close");
        }

        private sealed class FailingClosePresenter : Presenter
        {
            private readonly Exception _failure;

            public FailingClosePresenter(Exception failure) => _failure = failure;

            protected override void OnClose() => throw _failure;
        }

        private sealed class ListModelPresenter : Presenter<FakeView, List<int>>
        {
        }

        private sealed class IntModelPresenter : Presenter<FakeView, int>
        {
        }

        private sealed class NullableModelPresenter : Presenter<FakeView, int?>
        {
        }

        // Stands in for a UnityEngine.Object: once destroyed, it compares equal to null through Equals, exactly
        // as UnityEngine.Object.Equals does.
        private sealed class FakeUnityView
        {
            public bool Destroyed { get; set; }

            public override bool Equals(object other) => other == null ? Destroyed : ReferenceEquals(this, other);

            public override int GetHashCode() => 0;
        }

        private sealed class UnityLikePresenter : Presenter<FakeUnityView, int>
        {
            public int RefreshCount { get; private set; }

            protected override void OnRefresh() => RefreshCount++;
        }

        private sealed class InitializeProbe : Presenter
        {
            private readonly RecordingFactory _factory;

            public InitializeProbe(RecordingFactory factory) => _factory = factory;

            public bool WasInjectedBeforeInitialize { get; private set; }

            protected override void OnInitialize() => WasInjectedBeforeInitialize = _factory.Injected.Contains(this);
        }

        private sealed class ParentProbe : Presenter
        {
            public Presenter ParentSeen { get; private set; } = new TreePresenter("sentinel", new List<string>());

            protected override void OnInitialize() => ParentSeen = Parent;
        }

        // Wires one listener per attached view on ViewLifetime, the way a real presenter subscribes to a button.
        private sealed class WiringPresenter : Presenter<WiredView>
        {
            public List<string> Log { get; } = new List<string>();

            public Lifetime LastViewLifetime { get; private set; }

            public Action<WiringPresenter> WhenViewAdded { get; set; }

            public Lifetime ReadViewLifetime() => ViewLifetime;

            protected override void OnViewAdded()
            {
                var view = View;
                LastViewLifetime = ViewLifetime;
                view.Listeners++;
                ViewLifetime.AddAction(() => {
                    view.Listeners--;
                    Log.Add("view-scope-ended, View is " + (ReferenceEquals(View, view) ? "still the old view" : "changed"));
                });
                WhenViewAdded?.Invoke(this);
            }

            protected override void OnViewAfterRemoved() => Log.Add("view-removed");

            protected override void OnClose() => Log.Add("close");
        }

        private sealed class WiredView
        {
            public int Listeners { get; set; }
        }
    }
}
