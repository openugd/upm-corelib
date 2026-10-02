using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenUGD.Core.Presenters;

namespace OpenUGD.Tests
{
    // Specification source: ROADMAP-v2.md section 4.2 ("Presenters: OnReady is deleted").
    //
    // The contract under test:
    //   OnViewAdded()  fires exactly once, when a view attaches. Never again for the same view.
    //                  A replacement view attaches, so it fires again for that view.
    //   OnRefresh()    idempotent. Fires after the view attaches, and on every model change.
    //                  Never fires while the view is absent, or while the view is not alive.
    //   Liveness is a property of the presenter's own scope, not of the view type, so the whole suite
    //   runs headless with a fake view and no Unity runtime.
    //   Deleted for good: OnReady, Presenter.Internal.Ready, ISubscribeNotify,
    //   IPresenterWithModel.ModelChanged, OnAfterModelChanged. The reflection fixture at the bottom
    //   fails loudly if any of them is reintroduced.
    //
    // Written blind against the specification, then reconciled against the integrated package.
    // The one assumption that did not survive: Presenter.Root takes (Lifetime, Context), not a bare
    // Lifetime - a root with no Context cannot inject the tree. CreateRoot is the only line that
    // changed, exactly as the blind author predicted.
    [TestFixture]
    public class PresenterTests
    {
        private const BindingFlags DeclaredMembers = BindingFlags.Public
                                                     | BindingFlags.NonPublic
                                                     | BindingFlags.Instance
                                                     | BindingFlags.Static
                                                     | BindingFlags.DeclaredOnly;

        private List<Lifetime.Definition> _definitions;
        private Context _context;

        [SetUp]
        public void SetUp()
        {
            _definitions = new List<Lifetime.Definition>();
            _context = RunSync(() => {
                var builder = Context.CreateBuilder(NewDefinition().Lifetime);
                builder.Services.AddInstance<IProbe>(new Probe());
                return builder.BuildAsync();
            });
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
            _context = null;
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
        public void Attach_InjectsFromTheContextAndExposesIt()
        {
            var presenter = CreateRoot().AddPresenter(new InjectedPresenter());

            Assert.AreSame(_context, presenter.Context);
            Assert.IsNotNull(presenter.Probe, "Presenter.Internal.Initialize must inject from the Context");
            Assert.AreSame(_context.Resolve<IProbe>(), presenter.Probe);
        }

        [Test]
        public void Attach_LeavesAnUnresolvableOptionalMemberAlone()
        {
            // What TextPresenter/TMPPresenter rely on: a project with no localisation registered must still be
            // able to build its presenter tree.
            var presenter = CreateRoot().AddPresenter(new OptionallyInjectedPresenter());

            Assert.IsNull(presenter.Missing);
            Assert.AreSame(_context.Resolve<IProbe>(), presenter.Probe,
                "An optional member whose contract IS registered must still be injected.");
        }

        [Test]
        public void Attach_InjectsOptionalMembersBeforeOnInitialize()
        {
            // The ordering TextPresenter depends on: it subscribes to the injected ILocalizationChanged from
            // inside OnInitialize, so the field must already hold its value by then.
            var presenter = CreateRoot().AddPresenter(new OptionallyInjectedPresenter());

            Assert.AreSame(presenter.Probe, presenter.ProbeSeenDuringInitialize,
                "Presenter.Internal.Initialize must inject before it calls OnInitialize");
        }

        [Test]
        public void Attach_WithAnUnresolvableRequiredMember_ThrowsInsteadOfLeavingItNull()
        {
            var root = CreateRoot();

            Assert.Throws<ContextException>(() => root.AddPresenter(new RequiredMissingPresenter()));
        }

        [Test]
        public void ReadingLifetimeOrContextBeforeAttach_ThrowsInsteadOfReturningNull()
        {
            var presenter = new TreePresenter("x", new List<string>());

            Assert.Throws<InvalidOperationException>(() => { var _ = presenter.Lifetime; });
            Assert.Throws<InvalidOperationException>(() => { var _ = presenter.Context; });
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
        private Presenter CreateRoot(Lifetime lifetime) => new Presenter.Root(lifetime, _context);

        private static T RunSync<T>(Func<Task<T>> start, int timeoutMilliseconds = 15000)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var task = start();
                try
                {
                    if (!task.Wait(timeoutMilliseconds))
                    {
                        Assert.Fail("Operation did not complete within " + timeoutMilliseconds + " ms.");
                    }
                }
                catch (AggregateException)
                {
                }

                return task.GetAwaiter().GetResult();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

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

        private interface IProbe
        {
        }

        private sealed class Probe : IProbe
        {
        }

        private sealed class InjectedPresenter : Presenter
        {
#pragma warning disable 649
            [Inject] private IProbe _probe;
#pragma warning restore 649

            public IProbe Probe => _probe;
        }

        private interface IUnregistered
        {
        }

        private sealed class OptionallyInjectedPresenter : Presenter
        {
#pragma warning disable 649
            [Inject(Optional = true)] private IProbe _probe;
            [Inject(Optional = true)] private IUnregistered _missing;
#pragma warning restore 649

            public IProbe Probe => _probe;
            public IUnregistered Missing => _missing;
            public IProbe ProbeSeenDuringInitialize { get; private set; }

            protected override void OnInitialize() => ProbeSeenDuringInitialize = _probe;
        }

        private sealed class RequiredMissingPresenter : Presenter
        {
#pragma warning disable 649
            [Inject] private IUnregistered _missing;
#pragma warning restore 649

            public IUnregistered Missing => _missing;
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
    }
}
