using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpenUGD.Logging.Tests
{
    // The logging tree as a plain .NET object: tag paths, the filters, fan-out to sinks, threads and teardown.
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

            Assert.DoesNotThrow(() => root.Info("nobody is listening"));
        }

        [Test]
        public void AWriteOnTheRoot_IsTaggedWithTheRootTag()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.Info("hello");

            CollectionAssert.AreEqual(new[] { new Record(LogFlags.Info, "app", "hello") }, sink.Records);
        }

        [Test]
        public void ANullRootTag_IsTheEmptyString_AndLeavesNoLeadingDot()
        {
            var root = new LogRoot();
            var sink = Attach(root);

            root.Info("x");
            root.WithTag("child").Info("y");
            root.WithTag("child").WithTag("grandchild").Info("z");

            CollectionAssert.AreEqual(new[] { "", "child", "child.grandchild" }, sink.Records.Select(r => r.Tag));
        }

        [Test]
        public void EachWriteMethod_WritesItsOwnLevel()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.Verbose("v");
            root.Info("i");
            root.Warn("w");
            root.Error("e");
            root.Debug("d");
            root.Fatal("f");

            CollectionAssert.AreEqual(
                new[] { LogFlags.Verbose, LogFlags.Info, LogFlags.Warning, LogFlags.Error, LogFlags.Debug, LogFlags.Fatal },
                sink.Records.Select(r => r.Flag));
        }

        [Test]
        public void EachWriteMethodOfADerivedLogger_WritesItsOwnLevel()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);
            var child = root.WithTag("child");

            child.Verbose("v").Info("i").Warn("w").Error("e").Debug("d").Fatal("f");

            CollectionAssert.AreEqual(
                new[] { LogFlags.Verbose, LogFlags.Info, LogFlags.Warning, LogFlags.Error, LogFlags.Debug, LogFlags.Fatal },
                sink.Records.Select(r => r.Flag));
            Assert.IsTrue(sink.Records.All(r => r.Tag == "app.child"));
        }

        [Test]
        public void TheSingleLetterWriteMethods_AreGone()
        {
            var names = typeof(ILog).GetMethods().Select(m => m.Name).ToList();

            CollectionAssert.IsEmpty(names.Intersect(new[] { "V", "I", "W", "E", "D", "F" }),
                "one way to write each level: the named methods");
        }

        [Test]
        public void Writes_ReturnTheLoggerTheyWereCalledOn()
        {
            // The root used to return a hidden inner logger, whose Flag silenced the whole tree when a
            // chained call set it.
            var root = new LogRoot("app");
            var child = root.WithTag("child");

            Assert.AreSame(root, root.Info("x"));
            Assert.AreSame(root, root.Log(LogFlags.Info, "t", "x"));
            Assert.AreSame(child, child.Warn("y"));
        }

        [Test]
        public void AFirstLevelChild_HasTheRootAsItsParent()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("child");
            var grandChild = child.WithTag("grandchild");

            Assert.IsNull(root.Parent);
            Assert.AreSame(root, child.Parent);
            Assert.AreSame(child, grandChild.Parent);
        }

        [Test]
        public void Tag_IsTheFullPath_ComputedOnceAndDeliveredAsIs()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);
            var child = root.WithTag("net").WithTag("http");

            child.Info("one");
            child.Info("two");

            Assert.AreEqual("app", root.Tag);
            Assert.AreEqual("app.net.http", child.Tag);
            Assert.AreSame(child.Tag, sink.Records[0].Tag, "the path is built when the logger is derived, not per write");
            Assert.AreSame(child.Tag, sink.Records[1].Tag);
        }

        [Test]
        public void WithTagOfAType_UsesTheTypesSimpleName()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.WithTag(typeof(LogRootTests)).Info("a");
            root.WithTag(typeof(List<int>)).Info("b");

            CollectionAssert.AreEqual(new[] { "app.LogRootTests", "app.List`1" }, sink.Records.Select(r => r.Tag));
        }

        [Test]
        public void WithTag_RejectsNullAndEmpty()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("child");

            Assert.Throws<ArgumentNullException>(() => root.WithTag((string)null));
            Assert.Throws<ArgumentNullException>(() => root.WithTag((Type)null));
            Assert.Throws<ArgumentException>(() => root.WithTag(""));
            Assert.Throws<ArgumentNullException>(() => child.WithTag((string)null));
            Assert.Throws<ArgumentNullException>(() => child.WithTag((Type)null));
            Assert.Throws<ArgumentException>(() => child.WithTag(""));
        }

        [Test]
        public void EverySink_GetsEveryRecord_InSubscriptionOrder()
        {
            var root = new LogRoot("app");
            var order = new List<string>();
            root.Subscribe(new Sink(order, "first"));
            root.Subscribe(new Sink(order, "second"));

            root.Info("one");
            root.WithTag("child").Error("two");

            CollectionAssert.AreEqual(new[] { "first:one", "second:one", "first:two", "second:two" }, order);
        }

        [Test]
        public void ASinkSubscribedAfterAChildWasDerived_StillReceivesTheChildsRecords()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("late");
            var sink = Attach(root);

            child.Info("x");

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

            quiet.Info("dropped");
            quiet.Warn("kept");
            grandChild.Info("dropped too");
            grandChild.Error("kept too");
            sibling.Info("sibling kept");

            CollectionAssert.AreEqual(new[] { "kept", "kept too", "sibling kept" }, sink.Records.Select(r => r.Message));
        }

        [Test]
        public void LogFlag_IsTheFlagIntersectedWithEveryAncestor_TheRootIncluded()
        {
            var root = new LogRoot("app");
            var child = root.WithTag("child");
            child.Flag = LogFlags.Warning | LogFlags.Error | LogFlags.Fatal;
            var grandChild = child.WithTag("grandchild");
            grandChild.Flag = LogFlags.Error | LogFlags.Fatal;

            Assert.AreEqual(LogFlags.Error | LogFlags.Fatal, grandChild.LogFlag);

            root.Flag = LogFlags.Fatal | LogFlags.Info;

            Assert.AreEqual(LogFlags.Fatal, grandChild.LogFlag, "the root's flag narrows every logger, and they say so");
            Assert.AreEqual(LogFlags.Fatal | LogFlags.Info, root.WithTag("other").LogFlag);
        }

        [Test]
        public void TheRootFlag_GatesTheWholeTree()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);
            root.Flag = LogFlags.Error;
            var child = root.WithTag("child");

            child.Warn("dropped");
            child.Error("kept");
            root.Info("dropped at the root too");

            CollectionAssert.AreEqual(new[] { "kept" }, sink.Records.Select(r => r.Message));
        }

        [Test]
        public void IsEnabled_AnswersForTheEffectiveLevels()
        {
            var root = new LogRoot("app") { Flag = LogFlags.Warning | LogFlags.Error };
            var child = root.WithTag("child");
            child.Flag = LogFlags.Error | LogFlags.Info;

            Assert.IsTrue(root.IsEnabled(LogFlags.Warning));
            Assert.IsFalse(root.IsEnabled(LogFlags.Info));
            Assert.IsTrue(child.IsEnabled(LogFlags.Error));
            Assert.IsFalse(child.IsEnabled(LogFlags.Info), "the root does not permit Info, so neither does the child");
            Assert.IsFalse(child.IsEnabled(LogFlags.Warning), "the child does not permit Warning");
            Assert.IsFalse(root.IsEnabled(LogFlags.Warning | LogFlags.Info), "every level named must be enabled");
            Assert.IsFalse(root.IsEnabled(0), "no level at all is never enabled");
        }

        [Test]
        public void TheFunnel_DropsARecordWithNoLevel()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.Log(0, "t", "nothing");
            root.Log(LogFlags.Info, null, "bridged");

            CollectionAssert.AreEqual(new[] { new Record(LogFlags.Info, null, "bridged") }, sink.Records);
        }

        [Test]
        public void ILog_IsNotDisposable_ButTheRootIs()
        {
            // A container disposes what it built and is IDisposable. A tagged logger built by a factory
            // registration was disposed with the context, and every later write on it threw.
            Assert.IsFalse(typeof(IDisposable).IsAssignableFrom(typeof(ILog)));
            Assert.IsFalse(new LogRoot("app").WithTag("child") is IDisposable);
            Assert.IsTrue(typeof(IDisposable).IsAssignableFrom(typeof(LogRoot)));
        }

        [Test]
        public void Subscribe_RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => new LogRoot("app").Subscribe(null));
        }

        [Test]
        public void Unsubscribe_StopsDeliveryToThatSinkOnly()
        {
            var root = new LogRoot("app");
            var kept = Attach(root);
            var removed = Attach(root);

            root.Unsubscribe(removed);
            root.Unsubscribe(removed);
            root.Unsubscribe(new Sink());
            root.Info("after");

            Assert.AreEqual(1, kept.Records.Count);
            Assert.AreEqual(0, removed.Records.Count);
        }

        [Test]
        public void ASinkSubscribedTwice_IsUnsubscribedOneOccurrenceAtATime()
        {
            var root = new LogRoot("app");
            var sink = new Sink();
            root.Subscribe(sink);
            root.Subscribe(sink);

            root.Info("twice");
            root.Unsubscribe(sink);
            root.Info("once");

            CollectionAssert.AreEqual(new[] { "twice", "twice", "once" }, sink.Records.Select(r => r.Message));
        }

        [Test]
        public void UnsubscribingFromInsideASink_FinishesTheCurrentRecord_AndStopsTheNext()
        {
            var root = new LogRoot("app");
            var order = new List<string>();
            var leaving = new Sink(order, "leaving");
            leaving.During = (_, __) => root.Unsubscribe(leaving);
            root.Subscribe(leaving);
            root.Subscribe(new Sink(order, "staying"));

            Assert.DoesNotThrow(() => root.Info("one"));
            root.Info("two");

            CollectionAssert.AreEqual(new[] { "leaving:one", "staying:one", "staying:two" }, order);
        }

        [Test]
        public void SubscribingFromInsideASink_TakesEffectFromTheNextRecord()
        {
            var root = new LogRoot("app");
            var late = new Sink();
            var first = new Sink();
            first.During = (_, message) => {
                if ((string)message == "one") root.Subscribe(late);
            };
            root.Subscribe(first);

            Assert.DoesNotThrow(() => root.Info("one"));
            root.Info("two");

            CollectionAssert.AreEqual(new[] { "two" }, late.Records.Select(r => r.Message));
        }

        [Test]
        public void WritingFromOtherThreads_WhileSinksComeAndGo_NeverThrows()
        {
            // The sink list was a List<T> read by the writer and changed by Subscribe/Unsubscribe; a
            // Task.Run writer got "Collection was modified".
            var root = new LogRoot("app");
            var steady = new CountingSink();
            root.Subscribe(steady);
            var child = root.WithTag("worker");
            const int writers = 4;
            const int perWriter = 20000;
            var start = new ManualResetEventSlim(false);

            var tasks = Enumerable.Range(0, writers).Select(_ => Task.Run(() => {
                start.Wait();
                for (var i = 0; i < perWriter; i++) child.Info(i);
            })).ToArray();

            start.Set();
            var churn = new CountingSink();
            var toggles = 0;
            while (!tasks.All(t => t.IsCompleted) && toggles < 200000)
            {
                root.Subscribe(churn);
                root.Unsubscribe(churn);
                toggles++;
            }

            Assert.DoesNotThrow(() => Task.WaitAll(tasks));
            Assert.AreEqual(writers * perWriter, steady.Count, "a sink that stayed subscribed sees every record");
        }

        [Test]
        public void Dispose_DetachesEverySink_AndIsSafeToCallTwice()
        {
            var root = new LogRoot("app");
            var sink = Attach(root);

            root.Dispose();
            Assert.DoesNotThrow(root.Dispose);
            root.Info("after");

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

            public Action<LogFlags, object> During { get; set; }

            public void Log(LogFlags flag, string tag, object message)
            {
                Records.Add(new Record(flag, tag, message));
                _order?.Add(_name + ":" + message);
                During?.Invoke(flag, message);
            }
        }

        private sealed class CountingSink : ILogSink
        {
            private int _count;

            public int Count => Volatile.Read(ref _count);

            public void Log(LogFlags flag, string tag, object message) => Interlocked.Increment(ref _count);
        }
    }
}
