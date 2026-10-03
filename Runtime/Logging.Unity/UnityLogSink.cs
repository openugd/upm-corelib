using UnityEngine;

namespace OpenUGD.Logging
{
    /// <summary>
    /// An <see cref="ILogSink"/> that writes each record to the Unity console, formatted as
    /// <c>tag-&gt;message</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Six levels into three.</b> Unity's console knows log, warning and error, so
    /// <see cref="LogFlags.Verbose"/>, <see cref="LogFlags.Info"/> and <see cref="LogFlags.Debug"/>
    /// all arrive as plain entries, and <see cref="LogFlags.Fatal"/> is indistinguishable from
    /// <see cref="LogFlags.Error"/>. The level is not written anywhere in the line, so a reader of the
    /// console cannot recover it — encode it in the tag or the message if it matters.
    /// </para>
    /// <para>
    /// <b>Nothing here is compiled out.</b> There is no <c>Conditional</c> attribute and no editor-only
    /// guard: in a release player, every record that survives <see cref="LogRoot.Flag"/> is still
    /// formatted and still handed to <see cref="UnityEngine.Debug"/>. Quieten a shipped build by
    /// narrowing <see cref="LogRoot.Flag"/>, which is tested before the record ever reaches a
    /// sink — not by expecting these calls to disappear.
    /// </para>
    /// <para>
    /// Stateless, so one instance can serve any number of loggers;
    /// <see cref="UnityLogSinkExtensions.UseUnityConsole"/> nevertheless creates its own each time.
    /// </para>
    /// </remarks>
    public class UnityLogSink : ILogSink
    {
        /// <summary>
        /// Writes one record to the Unity console as <c>tag-&gt;message</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see cref="LogFlags.Error"/> and <see cref="LogFlags.Fatal"/> go to
        /// <see cref="UnityEngine.Debug.LogError(object)"/>, <see cref="LogFlags.Warning"/> to
        /// <see cref="UnityEngine.Debug.LogWarning(object)"/>, and the other three to
        /// <see cref="UnityEngine.Debug.Log(object)"/>.
        /// </para>
        /// <para>
        /// <b>A flag that is not exactly one level is dropped in silence.</b> The dispatch is a
        /// <c>switch</c> over single values, so <see cref="LogFlags.All"/>, any combination of levels,
        /// and the empty flag match no case and write nothing. The loggers never produce such a flag;
        /// a direct call to <see cref="LogRoot.Log"/> can.
        /// </para>
        /// <para>
        /// The string is built before the console call, so a string is allocated — and
        /// <paramref name="message"/> rendered through <c>ToString</c> unless it is <c>null</c> — for
        /// every record that gets this far, in every build configuration. A <c>null</c> tag or message
        /// contributes nothing but leaves the arrow.
        /// </para>
        /// <para>
        /// The entry's stack trace is captured inside this method, so double-clicking it in the console
        /// opens this file; the code that wrote the line is a few frames further down.
        /// </para>
        /// </remarks>
        /// <param name="flag">The severity. Exactly one level, or the record is dropped.</param>
        /// <param name="tag">The tag path, written before the arrow.</param>
        /// <param name="message">The message, written after the arrow. May be <c>null</c>.</param>
        public void Log(LogFlags flag, string tag, object message)
        {
            switch (flag)
            {
                case LogFlags.Verbose:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LogFlags.Info:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LogFlags.Warning:
                    Debug.LogWarning($"{tag}->{message}");
                    break;
                case LogFlags.Error:
                    Debug.LogError($"{tag}->{message}");
                    break;
                case LogFlags.Debug:
                    Debug.Log($"{tag}->{message}");
                    break;
                case LogFlags.Fatal:
                    Debug.LogError($"{tag}->{message}");
                    break;
            }
        }
    }

    /// <summary>
    /// Attaches the Unity console to a <see cref="LogRoot"/> for the length of a
    /// <see cref="Lifetime"/>.
    /// </summary>
    public static class UnityLogSinkExtensions
    {
        /// <summary>
        /// Subscribes a fresh <see cref="UnityLogSink"/> to <paramref name="logger"/>, and
        /// unsubscribes it when <paramref name="lifetime"/> terminates.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The one line of boot code that makes a <see cref="LogRoot"/> visible: until some sink
        /// is attached, every write through the tree is a no-op.
        /// </para>
        /// <para>
        /// <b>Not idempotent.</b> Each call constructs and subscribes its own sink, and
        /// <see cref="LogRoot.Subscribe"/> does not deduplicate, so calling this twice on the same
        /// root prints every record to the console twice. Call it once, where the root is created.
        /// </para>
        /// <para>
        /// <b>An already-terminated lifetime attaches nothing</b>, as registering anything else on a
        /// terminated lifetime does nothing that outlives the call. Test <see cref="Lifetime.IsTerminated"/>
        /// first if you need to know.
        /// </para>
        /// <para>
        /// The sink is not returned, so terminating <paramref name="lifetime"/> is the only way to
        /// detach it. For finer control, construct a <see cref="UnityLogSink"/> yourself and hand
        /// it to <see cref="LogRoot.Subscribe"/>.
        /// </para>
        /// </remarks>
        /// <param name="logger">The root whose records should reach the Unity console.</param>
        /// <param name="lifetime">The scope the subscription lives inside — typically the context's, so
        /// the console stops receiving records when that context is torn down.</param>
        /// <exception cref="System.ArgumentNullException"><paramref name="logger"/> or
        /// <paramref name="lifetime"/> is <c>null</c>.</exception>
        public static void UseUnityConsole(this LogRoot logger, Lifetime lifetime)
        {
            if (logger == null)
                throw new System.ArgumentNullException(nameof(logger), $"{nameof(logger)} can't be null");
            if (lifetime == null)
                throw new System.ArgumentNullException(nameof(lifetime), $"{nameof(lifetime)} can't be null");
            if (lifetime.IsTerminated) return;

            var sink = new UnityLogSink();
            logger.Subscribe(sink);
            lifetime.AddAction(() => logger.Unsubscribe(sink));
        }
    }
}
