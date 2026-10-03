using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenUGD.Presenters;

namespace OpenUGD.Tests
{
    // ContextPresenterFactory is the ten lines that join two assemblies which no longer know each other: the
    // presenter tree (com.openugd.presenters, container-free) and OpenUGD.Context. What the tree promised before
    // the split, it must still deliver through the adapter: [Inject] members of a presenter attached with `new`
    // are filled in, optional ones are left alone, a missing required one fails the attach, and all of it
    // happens before OnInitialize. None of it needs the Unity runtime.
    [TestFixture]
    public class ContextPresenterFactoryTests
    {
        private Lifetime.Definition _definition;
        private Context _context;
        private Probe _probe;

        [SetUp]
        public void SetUp()
        {
            _definition = Lifetime.Eternal.DefineNested();
            _probe = new Probe();
            _context = RunSync(() => {
                var builder = Context.CreateBuilder(_definition.Lifetime);
                builder.Services.AddInstance<IProbe>(_probe);
                builder.Services.Add<ContextPresenterFactory>().As<IPresenterFactory>();
                return builder.BuildAsync();
            });
        }

        [TearDown]
        public void TearDown() => _definition.Terminate();

        [Test]
        public void APresenterAttachedWithNew_IsInjectedFromTheContext()
        {
            var presenter = CreateRoot().AddPresenter(new InjectedPresenter());

            Assert.AreSame(_probe, presenter.Probe);
        }

        [Test]
        public void AnUnresolvableOptionalMember_IsLeftAlone()
        {
            // What TextPresenter/TMPPresenter rely on: a project with no localisation registered must still be
            // able to build its presenter tree.
            var presenter = CreateRoot().AddPresenter(new OptionallyInjectedPresenter());

            Assert.IsNull(presenter.Missing);
            Assert.AreSame(_probe, presenter.Probe, "an optional member whose contract IS registered is injected");
        }

        [Test]
        public void Injection_HappensBeforeOnInitialize()
        {
            // The ordering the widgets' TextPresenter depends on: it subscribes to an optional injected service
            // from inside OnInitialize, so the field must already hold its value by then.
            var presenter = CreateRoot().AddPresenter(new OptionallyInjectedPresenter());

            Assert.AreSame(_probe, presenter.ProbeSeenDuringInitialize);
        }

        [Test]
        public void AnUnresolvableRequiredMember_FailsTheAttach()
        {
            var root = CreateRoot();

            Assert.Throws<ContextException>(() => root.AddPresenter(new RequiredMissingPresenter()));
        }

        [Test]
        public void Create_UsesConstructorInjection_AndFillsMembers_AndLeavesThePresenterUnattached()
        {
            var factory = new ContextPresenterFactory(_context);

            var presenter = (ConstructedPresenter)factory.Create(typeof(ConstructedPresenter));

            Assert.AreSame(_probe, presenter.FromConstructor);
            Assert.AreSame(_probe, presenter.FromMember);
            Assert.Throws<InvalidOperationException>(() => { var _ = presenter.Lifetime; },
                "Create builds; attaching is the caller's next step");

            Presenter.Attach(presenter, _definition.Lifetime.DefineNested(), factory);
            Assert.IsFalse(presenter.Lifetime.IsTerminated);
        }

        [Test]
        public void Create_RejectsWhatIsNotAPresenter()
        {
            var factory = new ContextPresenterFactory(_context);

            Assert.Throws<ArgumentNullException>(() => factory.Create(null));
            Assert.Throws<ArgumentException>(() => factory.Create(typeof(Probe)));
            Assert.Throws<ArgumentNullException>(() => factory.Inject(null));
            Assert.Throws<ArgumentNullException>(() => new ContextPresenterFactory(null));
        }

        [Test]
        public void RegisteredInTheContainer_ItServesThatContext()
        {
            var factory = _context.Resolve<IPresenterFactory>();

            Assert.IsInstanceOf<ContextPresenterFactory>(factory);
            var presenter = new Presenter.Root(_definition.Lifetime, factory).AddPresenter(new InjectedPresenter());
            Assert.AreSame(_probe, presenter.Probe);
        }

        private Presenter CreateRoot() =>
            new Presenter.Root(_definition.Lifetime.DefineNested(), new ContextPresenterFactory(_context));

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

        private interface IProbe
        {
        }

        private sealed class Probe : IProbe
        {
        }

        private interface IUnregistered
        {
        }

        private sealed class InjectedPresenter : Presenter
        {
#pragma warning disable 649
            [Inject] private IProbe _probe;
#pragma warning restore 649

            public IProbe Probe => _probe;
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

        private sealed class ConstructedPresenter : Presenter
        {
#pragma warning disable 649
            [Inject] private IProbe _member;
#pragma warning restore 649

            public ConstructedPresenter(IProbe probe) => FromConstructor = probe;

            public IProbe FromConstructor { get; }
            public IProbe FromMember => _member;
        }
    }
}
