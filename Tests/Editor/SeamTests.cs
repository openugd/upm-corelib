using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenUGD.Commands;
using OpenUGD.Core.Presenters;
using OpenUGD.Services.Commands;

namespace OpenUGD.Tests
{
    // The two seams the blind presenter suite could not reach, because both go through internals:
    //   1. Presenter.Internal.Initialize -> SetModel -> SetView is the open sequence of a presenter-opening
    //      service, and what replaced Presenter.Internal.Ready. The 0.6.x UI services that ran it left
    //      corelib in 2.0.0 (branch park/ui-services, future com.openugd.corelib.ui); the sequence stays
    //      pinned here because Presenter is what any such host builds on. If it ever stops producing
    //      exactly one wiring and one render with both halves present, deleting Ready was wrong and a host
    //      needs a push again.
    //   2. CommandMapper builds each command with Context.Instantiate and aggregates failures.
    [TestFixture]
    public class SeamTests
    {
        private Lifetime.Definition _definition;
        private Context _context;

        [SetUp]
        public void SetUp()
        {
            _definition = Lifetime.Eternal.DefineNested();
            _context = RunSync(() => {
                var builder = Context.CreateBuilder(_definition.Lifetime);
                builder.Services.AddInstance<Ledger>(new Ledger());
                builder.Services.AddCommandMap();
                return builder.BuildAsync();
            });
        }

        [TearDown]
        public void TearDown() => _definition.Terminate();

        [Test]
        public void ServiceOpenSequence_ProducesOneWiringAndOneRender_WithBothHalvesPresent()
        {
            // Exactly what the 0.6.x UIWindowService / UIHudService / UITooltipService did on open.
            var definition = _definition.Lifetime.DefineNested("open");
            var presenter = new Panel();

            Presenter.Internal.Initialize(_context, presenter, definition);

            ((IPresenterWithModel)presenter).SetModel("payload");
            var mediatorView = (IPresenterWithView)presenter;
            Assert.AreEqual(typeof(FakeView), mediatorView.ViewType);
            mediatorView.SetView(new FakeView());

            CollectionAssert.AreEqual(new[] { "initialize", "view-added", "refresh:payload" }, presenter.Log,
                "the model must already be in place when the first render runs");
        }

        [Test]
        public void ServiceOpenSequence_OnATerminatedScope_Throws_SoTheGuardMustComeFirst()
        {
            var definition = _definition.Lifetime.DefineNested("open");
            definition.Terminate();

            Assert.Throws<InvalidOperationException>(
                () => Presenter.Internal.Initialize(_context, new Panel(), definition),
                "a host must check IsTerminated before attaching, and skip the whole open, for this reason");
        }

        [Test]
        public void CommandMapper_BuildsEachCommandFromTheContext_AndOffersTheMessageAndScope()
        {
            _context.MapCommand().Map<Ping, RecordCommand>();

            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));

            CollectionAssert.AreEqual(new[] { "one", "two" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void CommandMapper_OneTime_UnregistersAfterTheFirstMessage()
        {
            _context.MapCommand().Map<Ping, RecordCommand>(oneTime: true);

            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));

            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void CommandMapper_RunsEveryCommandEvenWhenOneThrows_AndAggregates()
        {
            var map = _context.MapCommand();
            map.Map<Ping, ThrowingCommand>();
            map.Map<Ping, RecordCommand>();

            var failure = Assert.Throws<AggregateException>(() => _context.Tell(new Ping("one")));

            Assert.AreEqual(1, failure.Flatten().InnerExceptions.Count);
            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries,
                "a throwing command must not stop the ones registered after it");
        }

        [Test]
        public void CommandMapper_RejectsANonCommandType_AtRegistrationRatherThanAtDispatch()
        {
            var mapper = _context.MapCommand().Map<Ping>();

            Assert.Throws<ArgumentException>(() => mapper.RegisterCommand(typeof(Ledger)));
        }

        private static T RunSync<T>(Func<Task<T>> start, int timeoutMilliseconds = 15000)
        {
            var previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                var task = start();
                try
                {
                    if (!task.Wait(timeoutMilliseconds)) Assert.Fail("did not complete");
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

        public sealed class Ledger
        {
            public List<string> Entries { get; } = new List<string>();
        }

        public sealed class Ping : IMessage
        {
            public Ping(string text) => Text = text;

            public string Text { get; }
        }

        public sealed class RecordCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly Ping _message;

            public RecordCommand(Ping message, Ledger ledger, Lifetime lifetime)
            {
                _message = message;
                _ledger = ledger;
                Assert.IsNotNull(lifetime, "the registration's Lifetime must be offered as an argument");
            }

            public void Execute() => _ledger.Entries.Add(_message.Text);
        }

        public sealed class ThrowingCommand : ICommand
        {
            public void Execute() => throw new InvalidOperationException("boom");
        }

        private sealed class FakeView
        {
            public bool IsAlive => true;
        }

        private sealed class Panel : Presenter<FakeView, string>
        {
            public List<string> Log { get; } = new List<string>();

            protected override void OnInitialize() => Log.Add("initialize");

            protected override void OnViewAdded() => Log.Add("view-added");

            protected override void OnRefresh() => Log.Add("refresh:" + Model);
        }
    }
}
