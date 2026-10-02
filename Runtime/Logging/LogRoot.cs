using System;
using System.Collections.Generic;

namespace OpenUGD.Logging
{
    /// <summary>
    /// The root of a logging tree and its fan-out point: every record written through this logger, or
    /// through any logger derived from it with <see cref="WithTag(string)"/>, is offered to each
    /// subscribed <see cref="ILogSink"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Nothing is written until a provider is attached.</b> A fresh instance has no subscribers, so
    /// every write is a no-op that still evaluates its argument. Attach the Unity console with
    /// <c>UnityLogSinkExtensions.UseUnityConsole</c> from the <c>com.openugd.logging.unity</c> assembly, or
    /// your own sink with <see cref="Subscribe"/>.
    /// </para>
    /// <para>
    /// <b>One gate for the whole tree.</b> A derived logger filters against its own chain of
    /// <see cref="ILog.Flag"/> values first, but every record that survives still funnels through
    /// <see cref="Log"/>, which re-tests it against this object's <see cref="Flag"/>. Narrowing
    /// <see cref="Flag"/> silences the whole tree, whatever the individual loggers permit.
    /// </para>
    /// <para>
    /// <b>The write methods do not return <c>this</c>.</b> <see cref="V"/> and its five siblings delegate
    /// to an internal root logger and return <i>that</i>: chaining reads the same and the tag is the same,
    /// but the result is a different object with its own <see cref="ILog.Flag"/>. Keep the instance you
    /// constructed rather than whatever a write handed back.
    /// </para>
    /// <para>
    /// <b>Not thread-safe, and not re-entrant.</b> <see cref="Subscribe"/>, <see cref="Unsubscribe"/> and
    /// <see cref="Log"/> share one unguarded list: subscribe during start-up, from a single thread, and
    /// never from inside <see cref="ILogSink.Log"/>. Delivery itself is synchronous, on the thread
    /// that wrote the record.
    /// </para>
    /// <para>
    /// <b><see cref="Dispose"/> throws.</b> It is unimplemented and exists only because
    /// <see cref="ILog"/> extends <see cref="IDisposable"/>; see the member.
    /// </para>
    /// </remarks>
    public class LogRoot : ILog
    {
        private readonly TaggedLog _impl;
        private readonly List<ILogSink> _sinks = new();

        /// <summary>
        /// Creates a root logger with no providers attached and every level enabled.
        /// </summary>
        /// <param name="tag">
        /// The first segment of every tag path this tree produces — an application or subsystem name.
        /// <c>null</c> means the empty string, which is the default and which leaves derived loggers with
        /// a leading dot in their path (<c>".Inventory"</c>) and records written on this logger itself
        /// tagged with the empty string. Pass a real name if anything reads the tag.
        /// </param>
        public LogRoot(string tag = null) => _impl = new TaggedLog(tag ?? "", null, this);

        /// <summary>
        /// Always <c>null</c>: a global logger is the root of its tree, never derived from another logger.
        /// </summary>
        public ILog Parent { get; } = null;

        /// <summary>
        /// The single gate every record passes, whichever logger in the tree produced it:
        /// <see cref="Log"/> drops anything whose level is not set here. Defaults to
        /// <see cref="LogFlags.All"/>.
        /// </summary>
        /// <remarks>
        /// Independent of the flags on loggers returned by <see cref="WithTag(string)"/> — those narrow
        /// their own subtree only, and cannot widen past this one.
        /// </remarks>
        public LogFlags Flag { get; set; } = LogFlags.All;

        /// <summary>
        /// Identical to <see cref="Flag"/>: a root logger has no ancestors to intersect with, so nothing
        /// narrows the set of levels it permits.
        /// </summary>
        public LogFlags LogFlag => Flag;

        /// <summary>
        /// Derives a child logger tagged with the simple name of <paramref name="type"/> —
        /// <c>Type.Name</c>, so no namespace, and a generic type keeps its arity suffix.
        /// </summary>
        /// <param name="type">The type to name the child after; only its name is read.</param>
        /// <returns>A new logger, exactly as <see cref="WithTag(string)"/> — see there for what it costs
        /// and for why it must not be disposed.</returns>
        /// <exception cref="NullReferenceException"><paramref name="type"/> is <c>null</c>: the name is
        /// read without a check.</exception>
        public ILog WithTag(Type type) => _impl.WithTag(type);

        /// <summary>
        /// Derives a child logger whose tag path is this logger's tag, a dot, then <paramref name="tag"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The child starts at <see cref="LogFlags.All"/>. A write survives if its level is set on the
        /// child and on every logger the child was derived from, and is then re-tested against
        /// <see cref="Flag"/> at the funnel. Its records reach the providers subscribed here, including
        /// ones subscribed after it was derived: on every write the child walks up to this logger and
        /// reads the provider list as it stands then.
        /// </para>
        /// <para>
        /// <b>Costs an allocation per record.</b> The effective level is recomputed by walking the chain
        /// to the root on every write, and the tag path is rebuilt — one string per level — for every
        /// record that survives the child's own filter, so a deeply derived logger is not free. Derive
        /// once and hold the result, as a field, rather than per call site.
        /// </para>
        /// <para>
        /// <b>Do not dispose the result.</b> A derived logger holds no resources; disposing it only
        /// detaches it from its parent, which truncates its tag path and severs its route to this logger,
        /// so the next write on it throws <see cref="NullReferenceException"/>. Drop the reference
        /// instead.
        /// </para>
        /// </remarks>
        /// <param name="tag">The segment to append. Not validated — <c>null</c> or <c>""</c> yields a path
        /// ending in a bare dot rather than an error.</param>
        /// <returns>A new logger. Every call allocates one, and the tree keeps no registry of them.
        /// </returns>
        public ILog WithTag(string tag) => _impl.WithTag(tag);

        /// <summary>
        /// Detaches every sink, so nothing written afterwards reaches anywhere. Safe to call more than
        /// once, and safe on a root that never had a sink.
        /// </summary>
        /// <remarks>
        /// <b>Changed in 2.0.0.</b> This used to throw <c>NotImplementedException</c>, deliberately: the
        /// argument was that failing loudly beats implying a teardown that does not exist. That argument
        /// stopped holding when the container underneath changed. <c>OpenUGD.Context</c> registers
        /// every constructed service that implements <see cref="IDisposable"/> for disposal when its
        /// lifetime ends, and <see cref="ILog"/> extends <see cref="IDisposable"/> — so a root registered
        /// with <c>Add&lt;LogRoot&gt;()</c> threw while the context was tearing down, turning an ordinary
        /// shutdown into a failure. Detaching the sinks is a real teardown, so the honest thing to do is
        /// perform it.
        /// </remarks>
        public void Dispose() => _sinks.Clear();

        /// <summary>
        /// Writes at <see cref="LogFlags.Verbose"/>, under this logger's root tag.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>. Each provider decides how it is
        /// rendered.</param>
        /// <returns>A logger over the same tag and the same providers, for chaining — <b>not</b> this
        /// instance; see the type remarks.</returns>
        public virtual ILog V(object message) => _impl.V(message);

        /// <summary>
        /// Writes at <see cref="LogFlags.Info"/>, under this logger's root tag.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>A logger for chaining — not this instance; see the type remarks.</returns>
        public virtual ILog I(object message) => _impl.I(message);

        /// <summary>
        /// Writes at <see cref="LogFlags.Warning"/>, under this logger's root tag.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>A logger for chaining — not this instance; see the type remarks.</returns>
        public virtual ILog W(object message) => _impl.W(message);

        /// <summary>
        /// Writes at <see cref="LogFlags.Error"/>, under this logger's root tag.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>A logger for chaining — not this instance; see the type remarks.</returns>
        public virtual ILog E(object message) => _impl.E(message);

        /// <summary>
        /// Writes at <see cref="LogFlags.Debug"/>, under this logger's root tag.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>A logger for chaining — not this instance; see the type remarks.</returns>
        public virtual ILog D(object message) => _impl.D(message);

        /// <summary>
        /// Writes at <see cref="LogFlags.Fatal"/>, under this logger's root tag. Writing it neither
        /// stops the process nor changes anything else; the level is a label the providers act on.
        /// </summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>A logger for chaining — not this instance; see the type remarks.</returns>
        public virtual ILog F(object message) => _impl.F(message);

        /// <summary>
        /// Attaches a sink. From here on, every record that passes <see cref="Flag"/> is handed to
        /// <paramref name="sink"/>, in subscription order relative to the other sinks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Neither deduplicated nor null-checked: subscribing the same provider twice makes it receive
        /// every record twice, and a <c>null</c> provider is accepted here and throws
        /// <see cref="NullReferenceException"/> at the next write.
        /// </para>
        /// <para>
        /// <b>Not re-entrant.</b> <see cref="Log"/> iterates the sink list directly, so subscribing
        /// from inside <see cref="ILogSink.Log"/> throws <see cref="InvalidOperationException"/>
        /// out of the enclosing loop. Nor is it thread-safe against a concurrent write.
        /// </para>
        /// </remarks>
        /// <param name="sink">The sink to attach. Records already written are not replayed to it.</param>
        public void Subscribe(ILogSink sink) => _sinks.Add(sink);

        /// <summary>
        /// Detaches a sink attached earlier with <see cref="Subscribe"/>, and does nothing if it was never
        /// attached.
        /// </summary>
        /// <remarks>
        /// Removes a single occurrence: a sink subscribed twice keeps receiving records until it is
        /// unsubscribed twice. Carries the same re-entrancy and threading caveats as
        /// <see cref="Subscribe"/> — calling it from inside <see cref="ILogSink.Log"/> breaks the
        /// loop that is delivering the record.
        /// </remarks>
        /// <param name="sink">The sink to detach, matched with the default equality comparer — reference
        /// equality, unless the sink overrides <c>Equals</c>.</param>
        public void Unsubscribe(ILogSink sink) => _sinks.Remove(sink);

        /// <summary>
        /// The funnel every record passes through: if <paramref name="flag"/> is permitted by
        /// <see cref="Flag"/>, hands the record to each subscribed provider in subscription order;
        /// otherwise does nothing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The loggers derived with <see cref="WithTag(string)"/> reach the providers through here: they
        /// resolve their tag path and apply their own filter first, then delegate to this method.
        /// Calling it directly is legitimate for bridging a foreign logging front end, and bypasses
        /// per-logger filtering: only <see cref="Flag"/> applies, and <paramref name="tag"/> is whatever
        /// you say it is.
        /// </para>
        /// <para>
        /// The test is <c>(Flag &amp; flag) == flag</c>, so a <paramref name="flag"/> naming several levels
        /// passes only when all of them are enabled, and one naming none passes always and reaches the
        /// providers with no level set.
        /// </para>
        /// <para>
        /// Nothing here catches: a provider that throws aborts delivery, so the providers after it in
        /// subscription order do not see the record, and the exception surfaces at the call site that
        /// wrote the log line.
        /// </para>
        /// </remarks>
        /// <param name="flag">The severity of the record; normally exactly one level.</param>
        /// <param name="tag">The tag path to attribute the record to. Passed to the providers unchanged,
        /// <c>null</c> included.</param>
        /// <param name="message">The record itself, not formatted here — each provider decides. May be
        /// <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        public ILog Log(LogFlags flag, string tag, object message)
        {
            if ((Flag & flag) == flag)
            {
                foreach (var logger in _sinks)
                {
                    logger.Log(flag, tag, message);
                }
            }

            return this;
        }

        private class TaggedLog : ILog
        {
            private readonly LogRoot _logRoot;
            private TaggedLog _parent;

            public TaggedLog(string tag, TaggedLog parent = null, LogRoot logRoot = null)
            {
                Tag = tag;
                _parent = parent;
                _logRoot = logRoot;
            }

            public string Tag { get; }

            private LogFlags InternalFlag {
                get {
                    if (_parent != null)
                    {
                        return _parent!.InternalFlag & Flag;
                    }

                    return Flag;
                }
            }

            private string InternalTag {
                get {
                    if (_parent != null)
                    {
                        return $"{_parent.InternalTag}.{Tag}";
                    }

                    return Tag;
                }
            }

            private LogRoot Global {
                get {
                    var current = this;
                    while (current != null)
                    {
                        if (current._logRoot != null)
                        {
                            return current._logRoot;
                        }

                        current = current._parent;
                    }

                    return null;
                }
            }

            public LogFlags Flag { get; set; } = LogFlags.All;
            public LogFlags LogFlag => InternalFlag;
            public ILog Parent => _parent;

            public ILog WithTag(Type type) => WithTag(type.Name);

            public ILog WithTag(string tag) => new TaggedLog(tag, this);

            public void Dispose() => _parent = null;

            public virtual ILog V(object message) => Log(LogFlags.Verbose, message);

            public virtual ILog I(object message) => Log(LogFlags.Info, message);

            public virtual ILog W(object message) => Log(LogFlags.Warning, message);

            public virtual ILog E(object message) => Log(LogFlags.Error, message);

            public virtual ILog D(object message) => Log(LogFlags.Debug, message);

            public virtual ILog F(object message) => Log(LogFlags.Fatal, message);

            private ILog Log(LogFlags flag, object message)
            {
                if ((InternalFlag & flag) == flag)
                {
                    Global.Log(flag, InternalTag, message);
                }

                return this;
            }
        }
    }
}
