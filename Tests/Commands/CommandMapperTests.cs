using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenUGD.Commands.Tests
{
    // CommandMapper builds each command with Context.Instantiate, offering the message and the registration's
    // scope as constructor arguments, and aggregates failures.
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
    }
}
