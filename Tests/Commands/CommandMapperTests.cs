using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenUGD.Commands.Tests
{
    // CommandMapper builds each command with Context.Instantiate - offering the message, the registration and the
    // execution's lifetime as constructor arguments - or with a registered factory, and aggregates failures. A
    // registration is checked when it is made and undone by terminating what it returns (CC-22, UH-16).
    [TestFixture]
    public class CommandMapperTests
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

        // Found in review: a struct with a satisfiable constructor passed the registration check, and
        // Context.Instantiate refuses every value type, so each Tell failed instead.
        [Test]
        public void Register_AStructCommand_ThrowsThen_NotAtTell()
        {
            var mapper = _context.MapCommand().Map<Ping>();

            Assert.Throws<ArgumentException>(() => mapper.RegisterCommand<StructCommand>());
            Assert.DoesNotThrow(() => _context.Tell(new Ping("nothing registered")));
        }

        // ------------------------------------------------------------------ undoing a registration (CC-22)

        [Test]
        public void Registration_TerminatingWhatRegisterReturns_Unregisters()
        {
            var registration = _context.MapCommand().Map<Ping, RecordCommand>();
            _context.Tell(new Ping("one"));

            registration.Terminate();
            _context.Tell(new Ping("two"));

            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Registration_IsDisposable_AndUndoesOnlyItself()
        {
            var map = _context.MapCommand();
            map.Map<Ping, RecordCommand>();
            using (map.Map<Ping, RecordCommand>())
            {
                _context.Tell(new Ping("both"));
            }

            _context.Tell(new Ping("one left"));

            CollectionAssert.AreEqual(new[] { "both", "both", "one left" }, _context.Resolve<Ledger>().Entries);
        }

        // ------------------------------------------------------------------ checked at registration (CC-22)

        [Test]
        public void Register_ACommandWhoseConstructorCannotBeSatisfied_ThrowsThen_NotAtTell()
        {
            var mapper = _context.MapCommand().Map<Ping>();

            var thrown = Assert.Throws<ArgumentException>(() => mapper.RegisterCommand<NeedsMissingCommand>());

            StringAssert.Contains(nameof(IMissing), thrown.Message, "the message names what cannot be resolved");
            Assert.DoesNotThrow(() => _context.Tell(new Ping("nothing registered")));
        }

        [Test]
        public void Register_ACommandWithTwoInjectConstructors_ThrowsThen()
        {
            Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, TwoInjectConstructorsCommand>());
        }

        [Test]
        public void Register_UsesTheWidestSatisfiableConstructor_AsInstantiateDoes()
        {
            // The widest constructor needs IMissing; the next one is satisfiable, so registration succeeds and Tell
            // builds the command with it.
            _context.MapCommand().Map<Ping, FallbackCommand>();

            _context.Tell(new Ping("fallback"));

            CollectionAssert.AreEqual(new[] { "fallback" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Register_AfterTheMapsScopeEnded_Throws()
        {
            var mapper = _context.MapCommand().Map<Ping>();
            _definition.Terminate();

            Assert.Throws<InvalidOperationException>(() => mapper.RegisterCommand<RecordCommand>());
            Assert.Throws<InvalidOperationException>(() => mapper.RegisterCommand((m, l) => new ThrowingCommand()));
        }

        // ------------------------------------------------------------------ a lifetime per execution (CC-22)

        [Test]
        public void Command_GetsAFreshLifetimePerExecution_EndedWhenExecuteReturns()
        {
            var registration = _context.MapCommand().Map<Ping, LifetimeProbeCommand>();
            var seen = _context.Resolve<Ledger>().Lifetimes;

            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));

            Assert.AreEqual(2, seen.Count);
            Assert.AreNotSame(seen[0], seen[1], "each execution has a scope of its own");
            Assert.IsTrue(seen.All(l => l.IsTerminated), "and it ends when the command returns");
            Assert.IsFalse(registration.IsTerminated, "while the registration lives on");
            CollectionAssert.AreEqual(new[] { "one:alive-in-execute", "one:cleaned-up", "two:alive-in-execute",
                "two:cleaned-up" }, _context.Resolve<Ledger>().Entries,
                "clean-up a command registers on its lifetime runs right after it, not when the registration ends");
        }

        [Test]
        public void Command_ExecutionLifetime_EndsEvenWhenExecuteThrows()
        {
            _context.MapCommand().Map<Ping, ThrowingLifetimeProbeCommand>();

            Assert.Throws<AggregateException>(() => _context.Tell(new Ping("boom")));

            Assert.IsTrue(_context.Resolve<Ledger>().Lifetimes.Single().IsTerminated);
        }

        [Test]
        public void Command_TheDefinitionOffered_IsItsRegistration_SoItCanUnregisterItself()
        {
            _context.MapCommand().Map<Ping, UnregisterSelfCommand>();

            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));

            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries);
        }

        // ------------------------------------------------------------------ factories: no reflection (UH-16)

        [Test]
        public void Factory_BuildsTheCommand_FromTheMessageAndAnExecutionLifetime()
        {
            var ledger = _context.Resolve<Ledger>();
            Lifetime execution = null;
            _context.MapCommand().Map<Ping>((message, lifetime) => {
                execution = lifetime;
                return PrivateCommand.Create(ledger, message.Text);
            });

            _context.Tell(new Ping("built by a factory"));

            CollectionAssert.AreEqual(new[] { "built by a factory" }, ledger.Entries,
                "a command with no public constructor, which Context.Instantiate could not build");
            Assert.IsTrue(execution.IsTerminated);
        }

        [Test]
        public void Factory_ReturningNull_FailsThatExecution_AndTheOthersStillRun()
        {
            var map = _context.MapCommand();
            map.Map<Ping>((message, lifetime) => null);
            map.Map<Ping, RecordCommand>();

            var thrown = Assert.Throws<AggregateException>(() => _context.Tell(new Ping("one")));

            Assert.IsInstanceOf<InvalidOperationException>(thrown.Flatten().InnerExceptions.Single());
            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Factory_OneTime_RunsOnce()
        {
            var ledger = _context.Resolve<Ledger>();
            _context.MapCommand().Map<Ping>((m, l) => PrivateCommand.Create(ledger, m.Text), oneTime: true);

            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));

            CollectionAssert.AreEqual(new[] { "one" }, ledger.Entries);
        }

        // ------------------------------------------------------------------ dispatch

        [Test]
        public void Tell_ACommandRegisteredDuringADispatch_RunsFromTheNextOne()
        {
            var ledger = _context.Resolve<Ledger>();
            var map = _context.MapCommand();
            var added = false;
            map.Map<Ping>((m, l) => {
                if (!added)
                {
                    added = true;
                    map.Map<Ping, RecordCommand>();
                }

                return PrivateCommand.Create(ledger, "first:" + m.Text);
            });

            _context.Tell(new Ping("a"));
            _context.Tell(new Ping("b"));

            CollectionAssert.AreEqual(new[] { "first:a", "first:b", "b" }, ledger.Entries);
        }

        [Test]
        public void Tell_RoutesByExactType_ABaseMappingDoesNotRunForADerivedMessage()
        {
            _context.MapCommand().Map<BasePing, RecordBaseCommand>();

            _context.Tell(new DerivedPing("derived"));
            _context.Tell(new BasePing("base"));

            CollectionAssert.AreEqual(new[] { "base" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Remove_UnregistersEveryRegistrationOfTheType()
        {
            var mapper = _context.MapCommand().Map<Ping>();
            mapper.RegisterCommand<RecordCommand>();
            mapper.RegisterCommand<RecordCommand>();

            ((ICommandMapperRemove)mapper).Remove<RecordCommand>();
            _context.Tell(new Ping("removed"));

            CollectionAssert.IsEmpty(_context.Resolve<Ledger>().Entries);
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

            public List<Lifetime> Lifetimes { get; } = new List<Lifetime>();
        }

        public interface IMissing
        {
        }

        public class BasePing : IMessage
        {
            public BasePing(string text) => Text = text;

            public string Text { get; }
        }

        public sealed class DerivedPing : BasePing
        {
            public DerivedPing(string text) : base(text)
            {
            }
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
                Assert.IsNotNull(lifetime, "the execution's Lifetime must be offered as an argument");
            }

            public void Execute() => _ledger.Entries.Add(_message.Text);
        }

        public sealed class RecordBaseCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly BasePing _message;

            public RecordBaseCommand(BasePing message, Ledger ledger)
            {
                _message = message;
                _ledger = ledger;
            }

            public void Execute() => _ledger.Entries.Add(_message.Text);
        }

        public struct StructCommand : ICommand
        {
            public StructCommand(Ping message)
            {
            }

            public void Execute()
            {
            }
        }

        public sealed class NeedsMissingCommand : ICommand
        {
            public NeedsMissingCommand(Ping message, IMissing missing)
            {
            }

            public void Execute()
            {
            }
        }

        public sealed class TwoInjectConstructorsCommand : ICommand
        {
            [Inject]
            public TwoInjectConstructorsCommand(Ping message)
            {
            }

            [Inject]
            public TwoInjectConstructorsCommand(Ledger ledger)
            {
            }

            public void Execute()
            {
            }
        }

        public sealed class FallbackCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly Ping _message;

            public FallbackCommand(Ping message, Ledger ledger, IMissing missing) : this(message, ledger)
            {
            }

            public FallbackCommand(Ping message, Ledger ledger)
            {
                _message = message;
                _ledger = ledger;
            }

            public void Execute() => _ledger.Entries.Add(_message.Text);
        }

        public sealed class LifetimeProbeCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly Lifetime _lifetime;
            private readonly Ping _message;

            public LifetimeProbeCommand(Ping message, Ledger ledger, Lifetime lifetime)
            {
                _message = message;
                _ledger = ledger;
                _lifetime = lifetime;
            }

            public void Execute()
            {
                _ledger.Lifetimes.Add(_lifetime);
                if (!_lifetime.IsTerminated) _ledger.Entries.Add(_message.Text + ":alive-in-execute");
                _lifetime.AddAction(() => _ledger.Entries.Add(_message.Text + ":cleaned-up"));
            }
        }

        public sealed class ThrowingLifetimeProbeCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly Lifetime _lifetime;

            public ThrowingLifetimeProbeCommand(Ledger ledger, Lifetime lifetime)
            {
                _ledger = ledger;
                _lifetime = lifetime;
            }

            public void Execute()
            {
                _ledger.Lifetimes.Add(_lifetime);
                throw new InvalidOperationException("boom");
            }
        }

        public sealed class UnregisterSelfCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly Ping _message;
            private readonly Lifetime.Definition _registration;

            public UnregisterSelfCommand(Ping message, Ledger ledger, Lifetime.Definition registration)
            {
                _message = message;
                _ledger = ledger;
                _registration = registration;
            }

            public void Execute()
            {
                _ledger.Entries.Add(_message.Text);
                _registration.Terminate();
            }
        }

        // No public constructor: only a factory can build it.
        public sealed class PrivateCommand : ICommand
        {
            private readonly Ledger _ledger;
            private readonly string _text;

            private PrivateCommand(Ledger ledger, string text)
            {
                _ledger = ledger;
                _text = text;
            }

            public static PrivateCommand Create(Ledger ledger, string text) => new PrivateCommand(ledger, text);

            public void Execute() => _ledger.Entries.Add(_text);
        }

        public sealed class ThrowingCommand : ICommand
        {
            public void Execute() => throw new InvalidOperationException("boom");
        }
    }
}
