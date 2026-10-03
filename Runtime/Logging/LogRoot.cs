using System;

namespace OpenUGD.Logging
{
    /// <summary>
    /// The root of a logging tree and its fan-out point: every record written through this logger, or
    /// through any logger derived from it with <see cref="WithTag(string)"/>, is offered to each
    /// subscribed <see cref="ILogSink"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is written until a sink is attached.</b> A fresh instance has no subscribers, so every
    /// write is a no-op that still evaluates its argument. Attach the Unity console with
    /// <c>UnityLogSinkExtensions.UseUnityConsole</c> from the <c>com.openugd.logging.unity</c> assembly, or
    /// your own sink with <see cref="Subscribe"/>.
    /// </para>
    /// <para>
    /// <b>One tree.</b> A logger derived here with <see cref="WithTag(string)"/> has this root as its
    /// <see cref="ILog.Parent"/>, and its <see cref="ILog.LogFlag"/> includes this root's <see cref="Flag"/>:
    /// narrowing <see cref="Flag"/> silences the whole tree, and every logger reports so.
    /// </para>
    /// <para>
    /// <b>Threads.</b> Writing from any thread is safe, and so is subscribing and unsubscribing while other
    /// threads write — including from inside <see cref="ILogSink.Log"/>. The sink list is copied on every
    /// change and swapped in whole, so a record in flight is delivered to the sinks that were subscribed when
    /// it started, and a change takes effect from the next record. No lock is held while a sink runs.
    /// </para>
    /// <para>
    /// <b>Failures.</b> Nothing here catches: a sink that throws aborts the delivery of that record, so the
    /// sinks after it in subscription order do not see it, and the exception surfaces at the call site that
    /// wrote the log line.
    /// </para>
    /// <para>
    /// <b><see cref="Dispose"/> detaches every sink.</b> The root keeps working afterwards, but nothing it
    /// is given reaches anywhere until a sink is subscribed again.
    /// </para>
    /// </remarks>
    public sealed class LogRoot : ILog, IDisposable
    {
        private readonly object _gate = new object();

        // Copy-on-write: replaced whole, under _gate, by Subscribe/Unsubscribe/Dispose; read without a lock.
        private volatile ILogSink[] _sinks = Array.Empty<ILogSink>();

        /// <summary>
        /// Creates a root logger with no sinks attached and every level enabled.
        /// </summary>
        /// <param name="tag">
        /// The first segment of every tag path this tree produces — an application or subsystem name.
        /// <c>null</c> means the empty string: records written on the root itself are then tagged with the
        /// empty string, and a derived logger's path starts with its own segment (<c>"Inventory"</c>, not
        /// <c>".Inventory"</c>).
        /// </param>
        public LogRoot(string tag = null) => Tag = tag ?? "";

        /// <summary>
        /// Always <c>null</c>: a root is never derived from another logger.
        /// </summary>
        public ILog Parent => null;

        /// <summary>
        /// The tag this root was created with, and the first segment of every path in its tree.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// The levels the whole tree permits. Defaults to <see cref="LogFlags.All"/>. A derived logger can
        /// narrow this for its own subtree, never widen it.
        /// </summary>
        public LogFlags Flag { get; set; } = LogFlags.All;

        /// <summary>
        /// Identical to <see cref="Flag"/>: a root has no ancestors to intersect with.
        /// </summary>
        public LogFlags LogFlag => Flag;

        /// <inheritdoc />
        public bool IsEnabled(LogFlags flag) => Permits(Flag, flag);

        /// <summary>
        /// Derives a child logger tagged with the simple name of <paramref name="type"/> —
        /// <c>Type.Name</c>, so no namespace, and a generic type keeps its arity suffix.
        /// </summary>
        /// <param name="type">The type to name the child after; only its name is read.</param>
        /// <returns>A new logger, exactly as <see cref="WithTag(string)"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
        public ILog WithTag(Type type) => new TaggedLog(this, null, NameOf(type));

        /// <summary>
        /// Derives a child logger whose tag path is this root's tag, a dot, then <paramref name="tag"/> —
        /// or just <paramref name="tag"/> under a root whose tag is empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The child starts at <see cref="LogFlags.All"/> and has this root as its <see cref="ILog.Parent"/>.
        /// A write on it is delivered if its level is set on the child and on every logger above it, this root
        /// included, and it reaches the sinks subscribed here at the time of the write, including ones
        /// subscribed after the child was derived.
        /// </para>
        /// <para>
        /// <b>Cost.</b> Deriving allocates the logger and its tag path, once. A write allocates nothing of its
        /// own: the level test walks up to the root comparing flags, and a dropped write stops there. Derive
        /// once and keep the result, in a field, rather than per call.
        /// </para>
        /// </remarks>
        /// <param name="tag">The segment to append. Must not be empty.</param>
        /// <returns>A new logger. Every call allocates one, and the tree keeps no registry of them.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="tag"/> is empty.</exception>
        public ILog WithTag(string tag) => new TaggedLog(this, null, SegmentOf(tag));

        /// <summary>Writes at <see cref="LogFlags.Verbose"/>, tagged with <see cref="Tag"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>. Each sink decides how it is rendered.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Verbose(object message) => Log(LogFlags.Verbose, Tag, message);

        /// <summary>Writes at <see cref="LogFlags.Info"/>, tagged with <see cref="Tag"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Info(object message) => Log(LogFlags.Info, Tag, message);

        /// <summary>Writes at <see cref="LogFlags.Warning"/>, tagged with <see cref="Tag"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Warn(object message) => Log(LogFlags.Warning, Tag, message);

        /// <summary>Writes at <see cref="LogFlags.Error"/>, tagged with <see cref="Tag"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Error(object message) => Log(LogFlags.Error, Tag, message);

        /// <summary>Writes at <see cref="LogFlags.Debug"/>, tagged with <see cref="Tag"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Debug(object message) => Log(LogFlags.Debug, Tag, message);

        /// <summary>Writes at <see cref="LogFlags.Fatal"/>, tagged with <see cref="Tag"/>. Writing it neither
        /// stops the process nor changes anything else; the level is a label the sinks act on.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Fatal(object message) => Log(LogFlags.Fatal, Tag, message);

        /// <summary>
        /// Attaches a sink. From the next record on, every record the tree delivers is handed to
        /// <paramref name="sink"/>, after the sinks subscribed before it.
        /// </summary>
        /// <remarks>
        /// Not deduplicated: subscribing the same sink twice makes it receive every record twice. Safe to call
        /// from any thread, and from inside <see cref="ILogSink.Log"/>; see the type remarks.
        /// </remarks>
        /// <param name="sink">The sink to attach. Records already written are not replayed to it.</param>
        /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <c>null</c>.</exception>
        public void Subscribe(ILogSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink), $"{nameof(sink)} can't be null");

            lock (_gate)
            {
                var current = _sinks;
                var next = new ILogSink[current.Length + 1];
                Array.Copy(current, next, current.Length);
                next[current.Length] = sink;
                _sinks = next;
            }
        }

        /// <summary>
        /// Detaches a sink attached earlier with <see cref="Subscribe"/>, and does nothing if it is not
        /// attached.
        /// </summary>
        /// <remarks>
        /// Removes a single occurrence, the earliest: a sink subscribed twice keeps receiving records until it
        /// is unsubscribed twice. A record already being delivered still reaches it. Safe to call from any
        /// thread, and from inside <see cref="ILogSink.Log"/>, including the sink's own.
        /// </remarks>
        /// <param name="sink">The sink to detach, matched with the default equality comparer — reference
        /// equality, unless the sink overrides <c>Equals</c>.</param>
        public void Unsubscribe(ILogSink sink)
        {
            lock (_gate)
            {
                var current = _sinks;
                var index = Array.IndexOf(current, sink);
                if (index < 0) return;

                var next = new ILogSink[current.Length - 1];
                Array.Copy(current, 0, next, 0, index);
                Array.Copy(current, index + 1, next, index, current.Length - index - 1);
                _sinks = next;
            }
        }

        /// <summary>
        /// The funnel: if every level in <paramref name="flag"/> is permitted by <see cref="Flag"/>, hands the
        /// record to each subscribed sink in subscription order; otherwise does nothing.
        /// </summary>
        /// <remarks>
        /// For bridging a foreign logging front end. It bypasses the per-logger filters: only
        /// <see cref="Flag"/> applies, and <paramref name="tag"/> is whatever you pass, <c>null</c> included.
        /// The loggers of the tree do not go through here; they test their own <see cref="ILog.LogFlag"/>, which
        /// already includes <see cref="Flag"/>, and deliver directly.
        /// </remarks>
        /// <param name="flag">The severity of the record; normally exactly one level. No level at all is never
        /// delivered.</param>
        /// <param name="tag">The tag path to attribute the record to.</param>
        /// <param name="message">The record itself, not formatted here — each sink decides. May be
        /// <c>null</c>.</param>
        /// <returns>This root, so calls chain.</returns>
        public ILog Log(LogFlags flag, string tag, object message)
        {
            if (Permits(Flag, flag)) Deliver(flag, tag, message);
            return this;
        }

        /// <summary>
        /// Detaches every sink, so nothing written afterwards reaches anywhere. Safe to call more than
        /// once, and on a root that never had a sink.
        /// </summary>
        /// <remarks>
        /// A context disposes every service it constructed that implements <see cref="IDisposable"/>, so a root
        /// registered with <c>Add&lt;LogRoot&gt;()</c> is detached when its context ends.
        /// </remarks>
        public void Dispose()
        {
            lock (_gate)
            {
                _sinks = Array.Empty<ILogSink>();
            }
        }

        private void Deliver(LogFlags flag, string tag, object message)
        {
            var sinks = _sinks;
            for (var i = 0; i < sinks.Length; i++)
            {
                sinks[i].Log(flag, tag, message);
            }
        }

        private static bool Permits(LogFlags enabled, LogFlags flag) => flag != 0 && (enabled & flag) == flag;

        private static string NameOf(Type type) =>
            type != null ? type.Name : throw new ArgumentNullException(nameof(type), $"{nameof(type)} can't be null");

        private static string SegmentOf(string tag)
        {
            if (tag == null) throw new ArgumentNullException(nameof(tag), $"{nameof(tag)} can't be null");
            if (tag.Length == 0)
                throw new ArgumentException("a tag segment can't be empty: it would leave a bare dot in the path",
                    nameof(tag));
            return tag;
        }

        private sealed class TaggedLog : ILog
        {
            private readonly LogRoot _root;
            private readonly TaggedLog _parent;

            public TaggedLog(LogRoot root, TaggedLog parent, string segment)
            {
                _root = root;
                _parent = parent;
                var above = parent != null ? parent.Tag : root.Tag;
                Tag = above.Length == 0 ? segment : above + "." + segment;
            }

            public ILog Parent => _parent != null ? (ILog)_parent : _root;

            public string Tag { get; }

            public LogFlags Flag { get; set; } = LogFlags.All;

            public LogFlags LogFlag => Flag & (_parent != null ? _parent.LogFlag : _root.Flag);

            public bool IsEnabled(LogFlags flag) => Permits(LogFlag, flag);

            public ILog WithTag(Type type) => new TaggedLog(_root, this, NameOf(type));

            public ILog WithTag(string tag) => new TaggedLog(_root, this, SegmentOf(tag));

            public ILog Verbose(object message) => Write(LogFlags.Verbose, message);

            public ILog Info(object message) => Write(LogFlags.Info, message);

            public ILog Warn(object message) => Write(LogFlags.Warning, message);

            public ILog Error(object message) => Write(LogFlags.Error, message);

            public ILog Debug(object message) => Write(LogFlags.Debug, message);

            public ILog Fatal(object message) => Write(LogFlags.Fatal, message);

            private ILog Write(LogFlags flag, object message)
            {
                if (IsEnabled(flag)) _root.Deliver(flag, Tag, message);
                return this;
            }
        }
    }
}
