using System;

namespace OpenUGD.Core.Logging
{
    /// <summary>
    /// A sink for log records. Attach one to a <see cref="LogRoot"/> with <c>LogRoot.Subscribe</c>.
    /// </summary>
    public interface ILogSink
    {
        /// <summary>Writes one record.</summary>
        /// <param name="flag">The severity of this record; exactly one flag.</param>
        /// <param name="tag">The dotted tag path of the logger that produced it; never <c>null</c>.</param>
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
    /// A tagged logging channel. Every write returns the same logger, so calls chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Breaking change in 2.0.0.</b> The six write methods took <c>dynamic message</c> and now take
    /// <c>object message</c>. This is source-compatible for every call site: a <c>dynamic</c> parameter is
    /// already <c>object</c> plus <c>[Dynamic]</c> in IL, so no signature changes and no implementer changes
    /// - <see cref="LogRoot"/> already declared <c>object</c>. What it removes is the obligation:
    /// <c>dynamic</c> forced a reference to <c>Microsoft.CSharp</c> on every assembly implementing this
    /// interface, and any implementer that actually used the parameter dynamically would have built a call
    /// site and thrown <c>ExecutionEngineException</c> under IL2CPP on device. Nothing in this repository
    /// did, so nothing was broken - but the trap was loaded and pointed at the log, which is exactly where a
    /// failure is least likely to be noticed.
    /// </para>
    /// <para>
    /// <b>Filtering.</b> A write is dropped unless its level is set in <see cref="LogFlag"/>, which is this
    /// logger's <see cref="Flag"/> intersected with every ancestor's.
    /// </para>
    /// </remarks>
    public interface ILog : IDisposable
    {
        /// <summary>The logger this one was derived from with <see cref="WithTag(string)"/>, or <c>null</c>
        /// at the root.</summary>
        ILog Parent { get; }

        /// <summary>The levels this logger permits, before its ancestors are taken into account.</summary>
        LogFlags Flag { get; set; }

        /// <summary>The levels actually written: <see cref="Flag"/> intersected with every
        /// ancestor's.</summary>
        LogFlags LogFlag { get; }

        /// <summary>Derives a child logger tagged with a type's name.</summary>
        /// <param name="type">The type to tag with.</param>
        ILog WithTag(Type type);

        /// <summary>Derives a child logger with an extra tag segment.</summary>
        /// <param name="tag">The segment, appended to this logger's tag path.</param>
        ILog WithTag(string tag);

        /// <summary>Writes at <see cref="LogFlags.Verbose"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog V(object message);

        /// <summary>Writes at <see cref="LogFlags.Info"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog I(object message);

        /// <summary>Writes at <see cref="LogFlags.Warning"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog W(object message);

        /// <summary>Writes at <see cref="LogFlags.Error"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog E(object message);

        /// <summary>Writes at <see cref="LogFlags.Debug"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog D(object message);

        /// <summary>Writes at <see cref="LogFlags.Fatal"/>.</summary>
        /// <param name="message">The message; may be <c>null</c>.</param>
        /// <returns>This logger, so calls chain.</returns>
        ILog F(object message);
    }
}
