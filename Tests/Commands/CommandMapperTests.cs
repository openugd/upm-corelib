using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenUGD.Commands.Tests
{
    // CommandMapper builds each command from a plan made at registration - offering the message, the registration and
    // the execution's lifetime as constructor arguments - or with a registered factory, and reports failures as the
    // family does: one as itself, several as one aggregate. A registration is checked when it is made and undone by
    // terminating what it returns (CC-22, UH-16).
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
        public void CommandMapper_RunsEveryCommandEvenWhenOneThrows()
        {
            var map = _context.MapCommand();
            map.Map<Ping, ThrowingCommand>();
            map.Map<Ping, RecordCommand>();

            Assert.Throws<InvalidOperationException>(() => _context.Tell(new Ping("one")));

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

            Assert.Throws<InvalidOperationException>(() => _context.Tell(new Ping("boom")));

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
                "a command with no public constructor, which a type registration could not build");
            Assert.IsTrue(execution.IsTerminated);
        }

        [Test]
        public void Factory_ReturningNull_FailsThatExecution_AndTheOthersStillRun()
        {
            var map = _context.MapCommand();
            map.Map<Ping>((message, lifetime) => null);
            map.Map<Ping, RecordCommand>();

            var thrown = Assert.Throws<InvalidOperationException>(() => _context.Tell(new Ping("one")));

            StringAssert.Contains("returned null", thrown.Message);
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

        // ------------------------------------------------------------------ one-time registrations (phase E)

        // A one-time command that told its own message from Execute ran twice: the registration was terminated only
        // after Execute returned, so the nested dispatch still found it live.
        [Test]
        public void OneTime_ACommandThatTellsItsOwnMessage_RunsOnce()
        {
            _context.MapCommand().Map<Ping, RetellCommand>(oneTime: true);

            _context.Tell(new Ping("first"));
            _context.Tell(new Ping("later"));

            CollectionAssert.AreEqual(new[] { "first" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void OneTime_AFactoryCommandThatTellsItsOwnMessage_RunsOnce()
        {
            var ledger = _context.Resolve<Ledger>();
            _context.MapCommand().Map<Ping>((message, lifetime) => new RetellCommand(message, ledger, _context),
                oneTime: true);

            _context.Tell(new Ping("first"));

            CollectionAssert.AreEqual(new[] { "first" }, ledger.Entries);
        }

        [Test]
        public void OneTime_ANestedDispatch_StillRunsTheOtherRegistrations()
        {
            var map = _context.MapCommand();
            map.Map<Ping, RetellCommand>(oneTime: true);
            map.Map<Ping, RecordCommand>();

            _context.Tell(new Ping("first"));

            CollectionAssert.AreEqual(new[] { "first", "retold", "first" }, _context.Resolve<Ledger>().Entries,
                "the nested Tell runs the permanent registration for the retold message, then the outer one resumes");
        }

        // ------------------------------------------------------------------ failures: the family policy (phase E)

        // Every failure used to arrive in an AggregateException, a single one included, and CommandMap wrapped the
        // mapper's aggregate in a second one.
        [Test]
        public void Tell_OneFailure_IsRethrownAsItself_WithTheStackTraceOfWhereItWasThrown()
        {
            _context.MapCommand().Map<Ping, ThrowingCommand>();

            var thrown = Assert.Throws<InvalidOperationException>(() => _context.Tell(new Ping("one")));

            StringAssert.Contains(nameof(ThrowingCommand), thrown.StackTrace);
        }

        [Test]
        public void Tell_TwoFailures_AreOneAggregate_OfTheFailuresThemselves()
        {
            var map = _context.MapCommand();
            map.Map<Ping, ThrowingCommand>();
            map.Map<Ping, ThrowingArgumentCommand>();

            var thrown = Assert.Throws<AggregateException>(() => _context.Tell(new Ping("one")));

            Assert.AreEqual(2, thrown.InnerExceptions.Count);
            Assert.IsInstanceOf<InvalidOperationException>(thrown.InnerExceptions[0]);
            Assert.IsInstanceOf<ArgumentException>(thrown.InnerExceptions[1]);
        }

        [Test]
        public void CommandMapper_TellDirectly_FollowsTheSamePolicy()
        {
            var mapper = new CommandMapper(_definition.Lifetime, typeof(Ping), _context);
            mapper.RegisterCommand<ThrowingCommand>();

            Assert.Throws<InvalidOperationException>(() => mapper.Tell(new Ping("one")));

            mapper.RegisterCommand<ThrowingArgumentCommand>();
            Assert.AreEqual(2, Assert.Throws<AggregateException>(() => mapper.Tell(new Ping("two"))).InnerExceptions.Count);
        }

        [Test]
        public void CommandMap_FailingCommandsAndAListener_ThrowOneFlatAggregate_CommandsFirst()
        {
            var map = (CommandMap)_context.MapCommand();
            map.Map<Ping, ThrowingCommand>();
            map.Map<Ping, ThrowingArgumentCommand>();
            map.Subscribe(_definition.Lifetime, new ThrowingListener());

            var thrown = Assert.Throws<AggregateException>(() => map.Tell(new Ping("one")));

            Assert.AreEqual(3, thrown.InnerExceptions.Count, "the mapper's failures are not nested in an aggregate of their own");
            CollectionAssert.AreEqual(
                new[] { typeof(InvalidOperationException), typeof(ArgumentException), typeof(NotSupportedException) },
                thrown.InnerExceptions.Select(e => e.GetType()).ToArray());
        }

        [Test]
        public void CommandMap_OneFailingListener_IsRethrownAsItself_AndTheCommandsStillRan()
        {
            var map = (CommandMap)_context.MapCommand();
            map.Map<Ping, RecordCommand>();
            map.Subscribe(_definition.Lifetime, new ThrowingListener());

            Assert.Throws<NotSupportedException>(() => map.Tell(new Ping("one")));

            CollectionAssert.AreEqual(new[] { "one" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Tell_AConstructorThatThrows_FailsWithThatException_AsAFactoryWould()
        {
            _context.MapCommand().Map<Ping, ThrowingConstructorCommand>();

            var thrown = Assert.Throws<FormatException>(() => _context.Tell(new Ping("one")));

            StringAssert.Contains(nameof(ThrowingConstructorCommand), thrown.StackTrace,
                "the stack trace is the constructor's, not the activator's rethrow");
        }

        // ------------------------------------------------------------------ no reflection per Tell (UH-16, phase E)

        // A type registration went through Context.Instantiate on every Tell, which inspects the type each time.
        // The plan is now made at registration: a Tell does not touch the command's Type at all.
        [Test]
        public void Tell_ATypeRegisteredCommand_DoesNotInspectItsTypeAgain()
        {
            var counting = new CountingType(typeof(RecordCommand));
            _context.MapCommand().Map<Ping>().RegisterCommand(counting);
            Assert.Greater(counting.Reads, 0, "the probe must see the registration's own inspection");

            counting.Reads = 0;
            _context.Tell(new Ping("one"));
            _context.Tell(new Ping("two"));
            _context.Tell(new Ping("three"));

            Assert.AreEqual(0, counting.Reads);
            CollectionAssert.AreEqual(new[] { "one", "two", "three" }, _context.Resolve<Ledger>().Entries);
        }

        [Test]
        public void Tell_AfterTheContextIsDisposed_BuildsNothing_AsInstantiateWould()
        {
            // A map that outlives its context: the registrations live on, the context does not.
            var outer = Lifetime.Eternal.DefineNested();
            try
            {
                var map = new CommandMap(outer.Lifetime, _context);
                map.Map<Ping>().RegisterCommand(typeof(StatelessCommand));
                StatelessCommand.Runs = 0;

                _context.Dispose();

                Assert.Throws<ObjectDisposedException>(() => map.Tell(new Ping("late")));
                Assert.AreEqual(0, StatelessCommand.Runs);
            }
            finally
            {
                outer.Terminate();
            }
        }

        [Test]
        public void Register_TwoEquallyWideSatisfiableConstructors_ThrowsThen_AsInstantiateWouldOnTell()
        {
            var thrown = Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, AmbiguousCommand>());

            StringAssert.Contains("ambiguous", thrown.Message);
        }

        // ------------------------------------------------------------------ [Inject] members (phase E)

        // [Inject] members were filled, and so first checked, only when the command was built on Tell.
        [Test]
        public void Register_AnUnsatisfiableInjectMember_ThrowsThen_NamingIt()
        {
            var mapper = _context.MapCommand().Map<Ping>();

            var thrown = Assert.Throws<ArgumentException>(() => mapper.RegisterCommand<MissingMemberCommand>());

            StringAssert.Contains(nameof(MissingMemberCommand.Missing), thrown.Message);
            StringAssert.Contains(nameof(IMissing), thrown.Message);
            Assert.DoesNotThrow(() => _context.Tell(new Ping("nothing registered")));
        }

        [Test]
        public void Register_AnInjectMemberOfTheMessageType_Throws_MembersComeFromTheContextOnly()
        {
            var thrown = Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, MessageMemberCommand>());

            StringAssert.Contains("constructor parameter instead", thrown.Message);
        }

        [Test]
        public void Register_AReadonlyInjectField_Throws()
        {
            Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, ReadonlyMemberCommand>());
        }

        [Test]
        public void Register_AnInjectPropertyWithoutASetter_Throws()
        {
            Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, GetterOnlyMemberCommand>());
        }

        [Test]
        public void Register_AnInjectMemberOfABaseClass_IsCheckedToo()
        {
            Assert.Throws<ArgumentException>(() => _context.MapCommand().Map<Ping, DerivedFromMissingMemberCommand>());
        }

        [Test]
        public void Tell_FillsInjectMembers_AndLeavesAnAbsentOptionalOneAsItWas()
        {
            _context.MapCommand().Map<Ping, MemberCommand>();

            _context.Tell(new Ping("members"));

            CollectionAssert.AreEqual(new[] { "members:fallback" }, _context.Resolve<Ledger>().Entries);
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
        public sealed class RetellCommand : ICommand
        {
            private readonly Context _context;
            private readonly Ledger _ledger;
            private readonly Ping _message;

            public RetellCommand(Ping message, Ledger ledger, Context context)
            {
                _message = message;
                _ledger = ledger;
                _context = context;
            }

            public void Execute()
            {
                _ledger.Entries.Add(_message.Text);
                if (_message.Text != "retold") _context.Tell(new Ping("retold"));
            }
        }

        public sealed class StatelessCommand : ICommand
        {
            public static int Runs;

            public void Execute() => Runs++;
        }

        public sealed class ThrowingArgumentCommand : ICommand
        {
            public void Execute() => throw new ArgumentException("bad");
        }

        public sealed class ThrowingConstructorCommand : ICommand
        {
            public ThrowingConstructorCommand(Ping message) => throw new FormatException("constructor");

            public void Execute()
            {
            }
        }

        public sealed class ThrowingListener : ITellMessage
        {
            public void Tell(object message) => throw new NotSupportedException("listener");
        }

        public sealed class AmbiguousCommand : ICommand
        {
            public AmbiguousCommand(Ping message)
            {
            }

            public AmbiguousCommand(Ledger ledger)
            {
            }

            public void Execute()
            {
            }
        }

        public class MissingMemberCommand : ICommand
        {
            [Inject] public IMissing Missing;

            public void Execute()
            {
            }
        }

        public sealed class DerivedFromMissingMemberCommand : MissingMemberCommand
        {
        }

        public sealed class MessageMemberCommand : ICommand
        {
            [Inject] private Ping _message;

            public void Execute()
            {
            }
        }

        public sealed class ReadonlyMemberCommand : ICommand
        {
            [Inject] public readonly Ledger Ledger;

            public void Execute()
            {
            }
        }

        public sealed class GetterOnlyMemberCommand : ICommand
        {
            [Inject] public Ledger Ledger => null;

            public void Execute()
            {
            }
        }

        public sealed class MemberCommand : ICommand
        {
            private readonly Ping _message;

            [Inject] private Ledger _ledger;
            [Inject(Optional = true)] private IMissing _missing = null;

            public MemberCommand(Ping message) => _message = message;

            [Inject] public Ledger AlsoLedger { get; private set; }

            public void Execute()
            {
                Assert.AreSame(_ledger, AlsoLedger);
                _ledger.Entries.Add(_message.Text + ":" + (_missing == null ? "fallback" : "injected"));
            }
        }

        // Counts how often the mapper inspects the command's type through this Type object.
        private sealed class CountingType : TypeDelegator
        {
            public int Reads;

            public CountingType(Type type) : base(type)
            {
            }

            public override Type BaseType
            {
                get
                {
                    Reads++;
                    return base.BaseType;
                }
            }

            protected override TypeAttributes GetAttributeFlagsImpl()
            {
                Reads++;
                return base.GetAttributeFlagsImpl();
            }

            public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
            {
                Reads++;
                return base.GetConstructors(bindingAttr);
            }

            public override FieldInfo[] GetFields(BindingFlags bindingAttr)
            {
                Reads++;
                return base.GetFields(bindingAttr);
            }

            public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
            {
                Reads++;
                return base.GetProperties(bindingAttr);
            }
        }
    }
}
