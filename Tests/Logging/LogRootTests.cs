using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace OpenUGD.Logging.Tests
{
    // The logging tree as a plain .NET object: tag paths, the two filters, fan-out to sinks, and teardown.
    // Nothing here needs Unity, which is the point of com.openugd.logging having no references at all.
    [TestFixture]
    public class LogRootTests
    {
        [Test]
        public void Assembly_ReferencesNeitherUnityNorAnotherOpenUGDAssembly()
        {
            var references = typeof(LogRoot).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            CollectionAssert.IsEmpty(
                references.Where(n => n.StartsWith("Unity", StringComparison.Ordinal) ||
                                      n.StartsWith("com.openugd", StringComparison.Ordinal)),
                "com.openugd.logging must stay engine-free and dependency-free");
        }

        [Test]
        public void AWrite_WithNoSinkAttached_GoesNowhere_AndDoesNotThrow()
        {
            var root = new LogRoot("app");

            Assert.DoesNotThrow(() => root.I("nobody is listening"));
        }

        [Test]
        public void AWriteOnTheRoot_IsTaggedWithTheRootTag()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.I("hello");

            CollectionAssert.AreEqual(new[] { new Record(LogFlags.Info, "app", "hello") }, sink.Records);
        }

        [Test]
        public void ANullRootTag_IsTheEmptyString()
        {
            var root = new LogRoot();
            var sink = Attach(root);

            root.I("x");
            root.WithTag("child").I("y");

            Assert.AreEqual("", sink.Records[0].Tag);
            Assert.AreEqual(".child", sink.Records[1].Tag);
        }

        [Test]
        public void EachWriteMethod_WritesItsOwnLevel()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.V("v");
            root.I("i");
            root.W("w");
            root.E("e");
            root.D("d");
            root.F("f");

            CollectionAssert.AreEqual(
                new[] { LogFlags.Verbose, LogFlags.Info, LogFlags.Warning, LogFlags.Error, LogFlags.Debug, LogFlags.Fatal },
                sink.Records.Select(r => r.Flag));
        }

        [Test]
        public void WithTag_BuildsADottedPathFromTheRoot()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.WithTag("net").WithTag("http").W("slow");

            CollectionAssert.AreEqual(new[] { new Record(LogFlags.Warning, "app.net.http", "slow") }, sink.Records);
        }

        [Test]
        public void WithTagOfAType_UsesTheTypesSimpleName()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.WithTag(typeof(LogRootTests)).I("a");
            root.WithTag(typeof(List<int>)).I("b");

            CollectionAssert.AreEqual(new[] { "app.LogRootTests", "app.List`1" }, sink.Records.Select(r => r.Tag));
        }

        [Test]
        public void EverySink_GetsEveryRecord_InSubscriptionOrder()
        {
            var root = new LogRoot("app");
            var order = new List<string>();
            root.Subscribe(new Sink(order, "first"));
            root.Subscribe(new Sink(order, "second"));

            root.I("one");
            root.WithTag("child").E("two");

            CollectionAssert.AreEqual(new[] { "first:one", "second:one", "first:two", "second:two" }, order);
        }

        [Test]
        public void ASinkSubscribedAfterAChildWasDerived_StillReceivesTheChildsRecords()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("late");
            var sink = Attach(root);

            child.I("x");

            CollectionAssert.AreEqual(new[] { new Record(LogFlags.Info, "app.late", "x") }, sink.Records);
        }

        [Test]
        public void AChildsFlag_FiltersItsOwnSubtree_AndLeavesItsSiblingsAlone()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);
            var quiet = root.WithTag("quiet");
            quiet.Flag = LogFlags.Warning | LogFlags.Error;
            var grandChild = quiet.WithTag("deeper");
            var sibling = root.WithTag("loud");

            quiet.I("dropped");
            quiet.W("kept");
            grandChild.I("dropped too");
            grandChild.E("kept too");
            sibling.I("sibling kept");

            CollectionAssert.AreEqual(new[] { "kept", "kept too", "sibling kept" }, sink.Records.Select(r => r.Message));
        }

        [Test]
        public void LogFlag_OfADerivedLogger_IsItsFlagIntersectedWithItsAncestors()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("child");
            child.Flag = LogFlags.Warning | LogFlags.Error;
            var grandChild = child.WithTag("grandchild");
            grandChild.Flag = LogFlags.Error | LogFlags.Fatal;

            Assert.AreEqual(LogFlags.Error, grandChild.LogFlag);
        }

        [Test]
        public void TheRootFlag_GatesTheWholeTree()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);
            root.Flag = LogFlags.Error;
            var child = root.WithTag("child");

            child.W("dropped");
            child.E("kept");
            root.I("dropped at the root too");

            CollectionAssert.AreEqual(new[] { "kept" }, sink.Records.Select(r => r.Message));
        }

        [Test]
        public void Unsubscribe_StopsDeliveryToThatSinkOnly()
        {
            var root = new LogRoot("app");
            var kept = Attach(root);
            var removed = Attach(root);

            root.Unsubscribe(removed);
            root.I("after");

            Assert.AreEqual(1, kept.Records.Count);
            Assert.AreEqual(0, removed.Records.Count);
        }

        [Test]
        public void Dispose_DetachesEverySink_AndIsSafeToCallTwice()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.Dispose();
            Assert.DoesNotThrow(root.Dispose);
            root.I("after");

            Assert.AreEqual(0, sink.Records.Count);
        }

        private static Sink Attach(LogRoot root)
        {
            var sink = new Sink();
            root.Subscribe(sink);
            return sink;
        }

        private readonly struct Record : IEquatable<Record>
        {
            public Record(LogFlags flag, string tag, object message)
            {
                Flag = flag;
                Tag = tag;
                Message = message;
            }

            public LogFlags Flag { get; }
            public string Tag { get; }
            public object Message { get; }

            public bool Equals(Record other) =>
                Flag == other.Flag && Tag == other.Tag && Equals(Message, other.Message);

            public override bool Equals(object obj) => obj is Record other && Equals(other);

            public override int GetHashCode() => (int)Flag ^ (Tag?.GetHashCode() ?? 0);

            public override string ToString() => Flag + " " + Tag + "->" + Message;
        }

        private sealed class Sink : ILogSink
        {
            private readonly List<string> _order;
            private readonly string _name;

            public Sink(List<string> order = null, string name = null)
            {
                _order = order;
                _name = name;
            }

            public List<Record> Records { get; } = new List<Record>();

            public void Log(LogFlags flag, string tag, object message)
            {
                Records.Add(new Record(flag, tag, message));
                _order?.Add(_name + ":" + message);
            }
        }
    }
}
