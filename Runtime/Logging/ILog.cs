using System;

namespace OpenUGD.Logging
{
    /// <summary>
    /// A sink for log records. Attach one to a <see cref="LogRoot"/> with <c>LogRoot.Subscribe</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Log"/> is called on the thread that wrote the record, possibly on several threads at once,
    /// so an implementation that keeps state must guard it.
    /// </remarks>
    public interface ILogSink
    {
        /// <summary>Writes one record.</summary>
        /// <param name="flag">The severity of this record; exactly one flag when it comes from a logger's write
        /// method.</param>
        /// <param name="tag">The dotted tag path of the logger that produced it; never <c>null</c> when it comes
        /// from a logger's write method.</param>
        /// <param name="message">The message. May be <c>null</c>, and may be any object.</param>
        void Log(LogFlags flag, string tag, object message);
    }

    /// <summary>
    /// Severity levels, as a bit set so that <see cref="ILog.Flag"/> can enable several at once.
    /// </summary>
    [Flags]
    public enum LogFlags
    {
        /// <summary>Tracing detail.</summary>
        Verbose = 1,

        /// <summary>Ordinary progress.</summary>
        Info = 1 << 1,

        /// <summary>Something suspicious that did not stop anything.</summary>
        Warning = 1 << 2,

        /// <summary>Something failed.</summary>
        Error = 1 << 3,

        /// <summary>Developer-only diagnostics.</summary>
        Debug = 1 << 4,

        /// <summary>Unrecoverable.</summary>
        Fatal = 1 << 5,

        /// <summary>Every level.</summary>
        All = Verbose | Info | Warning | Error | Debug | Fatal
    }

    /// <summary>
    /// A tagged logging channel: a <see cref="LogRoot"/>, or a logger derived from one with
    /// <see cref="WithTag(string)"/>. Every write returns the logger it was called on, so calls chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Filtering.</b> A write is dropped unless its level is set in <see cref="LogFlag"/>, which is this
    /// logger's <see cref="Flag"/> intersected with every ancestor's, the root's included. Ask
    /// <see cref="IsEnabled"/> before building an expensive message: the argument of a write is evaluated
    /// whether or not the write is dropped.
    /// </para>
    /// <para>
    /// <b>Threads.</b> Writing, deriving and changing <see cref="Flag"/> are safe from any thread. A record is
    /// delivered synchronously, on the thread that wrote it.
    /// </para>
    /// <para>
    /// <b>Not disposable.</b> A logger holds nothing to release, so a container that disposes what it built
    /// leaves loggers alone. <see cref="LogRoot"/>, which owns the sinks, is disposable on its own account.
    /// </para>
    /// </remarks>
    public interface ILog
    {
        /// <summary>The logger this one was derived from with <see cref="WithTag(string)"/>, or <c>null</c>
        /// for a <see cref="LogRoot"/>.</summary>
        ILog Parent { get; }

        /// <summary>The full dotted tag path every record written here carries — the root's tag, then each
        /// segment down to this logger. Computed once, when the logger is derived.</summary>
        string Tag { get; }

        /// <summary>The levels this logger permits, before its ancestors are taken into account. Starts at
        /// <see cref="LogFlags.All"/>.</summary>
        LogFlags Flag { get; set; }

        /// <summary>The levels actually written: <see cref="Flag"/> intersected with every ancestor's, the
        /// root's included.</summary>
        LogFlags LogFlag { get; }

        /// <summary>
        /// Whether a write at <paramref name="flag"/> would reach the sinks — every level it names is set in
        /// <see cref="LogFlag"/>. Use it to skip building a message nobody will receive.
        /// </summary>
        /// <param name="flag">Normally exactly one level. No level at all is never enabled.</param>
        /// <returns><c>true</c> if a write at <paramref name="flag"/> would be delivered.</returns>
        bool IsEnabled(LogFlags flag);

        /// <summary>Derives a child logger tagged with a type's simple name (<c>Type.Name</c>).</summary>
        /// <param name="type">The type to tag with.</param>
        /// <returns>A new logger whose <see cref="Parent"/> is this one.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <c>null</c>.</exception>
        ILog WithTag(Type type);

        /// <summary>Derives a child logger with an extra tag segment.</summary>
        /// <param name="tag">The segment, appended to this logger's <see cref="Tag"/> after a dot.</param>
        /// <returns>A new logger whose <see cref="Parent"/> is this one.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tag"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="tag"/> is empty.</exception>
        ILog WithTag(string tag);

        /// <summary>Writes at <see cref="LogFlags.Verbose"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Verbose(object message);

        /// <summary>Writes at <see cref="LogFlags.Info"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Info(object message);

        /// <summary>Writes at <see cref="LogFlags.Warning"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Warn(object message);

        /// <summary>Writes at <see cref="LogFlags.Error"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Error(object message);

        /// <summary>Writes at <see cref="LogFlags.Debug"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Debug(object message);

        /// <summary>Writes at <see cref="LogFlags.Fatal"/>. Writing it neither stops the process nor changes
        /// anything else; the level is a label the sinks act on.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog Fatal(object message);
    }
}
